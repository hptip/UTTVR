using UnityEngine;
using TMPro;
using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using ExcelDataReader;
using SFB;

public class ExcelImportManager : MonoBehaviour
{
    public TMP_Text statusText;

    // =====================================================
    // 1. IMPORT EXCEL
    // =====================================================

    public void ImportExcel()
    {
        if (RailwayDatabase.Instance == null)
        {
            SetStatus("Database chưa được khởi tạo!");
            return;
        }

        var extensions = new[]
        {
            new ExtensionFilter("Excel Files", "xlsx")
        };

        string[] paths = StandaloneFileBrowser.OpenFilePanel(
            "Select Excel File",
            "",
            extensions,
            false
        );

        if (paths == null || paths.Length == 0)
            return;

        try
        {
            List<string> headers;
            List<string> types;
            List<object[]> rows;

            // Đọc sheet đầu tiên
            ReadFirstSheet(
                paths[0],
                out headers,
                out types,
                out rows
            );

            if (rows.Count == 0)
                throw new Exception("Excel không có dữ liệu.");

            // Tìm vị trí ModelID
            int modelIndex = headers.FindIndex(h =>
                h.Equals(
                    "ModelID",
                    StringComparison.OrdinalIgnoreCase
                )
            );

            // Tìm vị trí MeshID
            int meshIndex = headers.FindIndex(h =>
                h.Equals(
                    "MeshID",
                    StringComparison.OrdinalIgnoreCase
                )
            );

            if (modelIndex < 0 || meshIndex < 0)
            {
                throw new Exception(
                    "Excel bắt buộc phải có hai cột ModelID và MeshID!"
                );
            }

            // =================================================
            // KIỂM TRA KHÓA CHÍNH KÉP
            // =================================================

            HashSet<string> keys = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase
            );

            foreach (var row in rows)
            {
                string modelID = Convert.ToString(
                    row[modelIndex],
                    CultureInfo.InvariantCulture
                )?.Trim();

                string meshID = Convert.ToString(
                    row[meshIndex],
                    CultureInfo.InvariantCulture
                )?.Trim();

                if (string.IsNullOrWhiteSpace(modelID))
                {
                    throw new Exception(
                        "Có dòng bị thiếu ModelID!"
                    );
                }

                if (string.IsNullOrWhiteSpace(meshID))
                {
                    throw new Exception(
                        "Có dòng bị thiếu MeshID!"
                    );
                }

                // Tạo khóa kiểm tra duy nhất từ hai giá trị
                string compositeKey =
                    modelID.Length + ":" + modelID + meshID;

                if (!keys.Add(compositeKey))
                {
                    throw new Exception(
                        "Khóa chính kép bị trùng!\n" +
                        "ModelID: " + modelID +
                        "\nMeshID: " + meshID
                    );
                }
            }

            // =================================================
            // IMPORT VÀO DATABASE
            // =================================================

            RailwayDatabase.Instance.ImportDynamicData(
                headers,
                types,
                rows,
                out int added,
                out int updated
            );

            SetStatus(
                "Import thành công!\n" +
                "File: " + Path.GetFileName(paths[0]) +
                "\nSheet: Sheet đầu tiên" +
                "\nTổng bản ghi: " + rows.Count +
                "\nThêm mới: " + added +
                "\nCập nhật: " + updated +
                "\nSố cột: " + headers.Count
            );
        }
        catch (Exception e)
        {
            Debug.LogException(e);

            SetStatus(
                "Import thất bại:\n" + e.Message
            );
        }
    }

    // =====================================================
    // 2. ĐỌC SHEET ĐẦU TIÊN
    // =====================================================

    private void ReadFirstSheet(
        string path,
        out List<string> headers,
        out List<string> types,
        out List<object[]> rows)
    {
        headers = new List<string>();
        types = new List<string>();
        rows = new List<object[]>();

        using (var stream = File.Open(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite
        ))
        using (var reader = ExcelReaderFactory.CreateReader(stream))
        {
            // ---------------------------------------------
            // ĐỌC HEADER
            // ---------------------------------------------

            if (!reader.Read())
                throw new Exception("Sheet đầu tiên trống.");

            for (int i = 0; i < reader.FieldCount; i++)
            {
                object value = reader.GetValue(i);

                string header = value == null
                    ? ""
                    : Convert.ToString(
                        value,
                        CultureInfo.InvariantCulture
                    ).Trim();

                if (string.IsNullOrWhiteSpace(header))
                {
                    throw new Exception(
                        "Cột thứ " + (i + 1) +
                        " chưa có tên."
                    );
                }

                headers.Add(header);
            }

            // ---------------------------------------------
            // KIỂM TRA HEADER TRÙNG
            // ---------------------------------------------

            if (headers.Distinct(
                StringComparer.OrdinalIgnoreCase
            ).Count() != headers.Count)
            {
                throw new Exception(
                    "Tên cột Excel bị trùng."
                );
            }

            // ---------------------------------------------
            // KIỂM TRA HAI CỘT KHÓA CHÍNH
            // ---------------------------------------------

            bool hasModelID = headers.Any(h =>
                h.Equals(
                    "ModelID",
                    StringComparison.OrdinalIgnoreCase
                )
            );

            bool hasMeshID = headers.Any(h =>
                h.Equals(
                    "MeshID",
                    StringComparison.OrdinalIgnoreCase
                )
            );

            if (!hasModelID || !hasMeshID)
            {
                throw new Exception(
                    "Không tìm thấy ModelID hoặc MeshID."
                );
            }

            // ---------------------------------------------
            // ĐỌC CÁC DÒNG DỮ LIỆU
            // ---------------------------------------------

            while (reader.Read())
            {
                object[] row = new object[headers.Count];

                bool empty = true;

                for (int i = 0; i < headers.Count; i++)
                {
                    object value = reader.GetValue(i);

                    row[i] = value;

                    if (value != null &&
                        !string.IsNullOrWhiteSpace(
                            Convert.ToString(
                                value,
                                CultureInfo.InvariantCulture
                            )
                        ))
                    {
                        empty = false;
                    }
                }

                if (!empty)
                    rows.Add(row);
            }
        }

        // =================================================
        // 3. NHẬN DIỆN KIỂU DỮ LIỆU
        // =================================================

        for (int col = 0; col < headers.Count; col++)
        {
            string header = headers[col];

            // ModelID và MeshID luôn là TEXT
            bool isKeyColumn =
                header.Equals(
                    "ModelID",
                    StringComparison.OrdinalIgnoreCase
                ) ||
                header.Equals(
                    "MeshID",
                    StringComparison.OrdinalIgnoreCase
                );

            bool hasValue = false;
            bool isNumeric = true;

            if (!isKeyColumn)
            {
                foreach (var row in rows)
                {
                    object value = row[col];

                    if (value == null ||
                        string.IsNullOrWhiteSpace(
                            Convert.ToString(
                                value,
                                CultureInfo.InvariantCulture
                            )
                        ))
                    {
                        continue;
                    }

                    hasValue = true;

                    if (!IsNumeric(value))
                    {
                        isNumeric = false;
                        break;
                    }
                }
            }
            else
            {
                isNumeric = false;
            }

            // Cột toàn bộ giá trị trống mặc định là TEXT
            bool useReal = hasValue && isNumeric;

            types.Add(useReal ? "REAL" : "TEXT");

            // =================================================
            // 4. CHUẨN HÓA DỮ LIỆU
            // =================================================

            for (int r = 0; r < rows.Count; r++)
            {
                object value = rows[r][col];

                if (value == null ||
                    string.IsNullOrWhiteSpace(
                        Convert.ToString(
                            value,
                            CultureInfo.InvariantCulture
                        )
                    ))
                {
                    rows[r][col] = null;
                }
                else if (useReal)
                {
                    if (!TryConvertNumeric(
                        value,
                        out double number))
                    {
                        throw new Exception(
                            "Không thể chuyển dữ liệu thành số tại cột: " +
                            header
                        );
                    }

                    rows[r][col] = number;
                }
                else
                {
                    rows[r][col] = Convert.ToString(
                        value,
                        CultureInfo.InvariantCulture
                    );
                }
            }
        }
    }

    // =====================================================
    // 5. KIỂM TRA GIÁ TRỊ CÓ PHẢI SỐ KHÔNG
    // =====================================================

    private bool IsNumeric(object value)
    {
        if (value == null)
            return false;

        if (value is byte ||
            value is short ||
            value is int ||
            value is long ||
            value is float ||
            value is double ||
            value is decimal)
        {
            return true;
        }

        string text = Convert.ToString(
            value,
            CultureInfo.InvariantCulture
        );

        return double.TryParse(
            text,
            NumberStyles.Any,
            CultureInfo.InvariantCulture,
            out _
        ) ||
        double.TryParse(
            text,
            NumberStyles.Any,
            CultureInfo.CurrentCulture,
            out _
        );
    }

    // =====================================================
    // 6. CHUYỂN DỮ LIỆU THÀNH DOUBLE
    // =====================================================

    private bool TryConvertNumeric(
        object value,
        out double result)
    {
        result = 0;

        if (value == null)
            return false;

        if (value is byte ||
            value is short ||
            value is int ||
            value is long ||
            value is float ||
            value is double ||
            value is decimal)
        {
            result = Convert.ToDouble(
                value,
                CultureInfo.InvariantCulture
            );

            return true;
        }

        string text = Convert.ToString(
            value,
            CultureInfo.InvariantCulture
        );

        if (double.TryParse(
            text,
            NumberStyles.Any,
            CultureInfo.InvariantCulture,
            out result))
        {
            return true;
        }

        return double.TryParse(
            text,
            NumberStyles.Any,
            CultureInfo.CurrentCulture,
            out result
        );
    }

    // =====================================================
    // 7. HIỂN THỊ TRẠNG THÁI
    // =====================================================

    private void SetStatus(string message)
    {
        Debug.Log(message);

        if (statusText != null)
            statusText.text = message;
    }
}