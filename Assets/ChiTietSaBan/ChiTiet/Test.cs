
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

public class RailwayDatabaseTest : MonoBehaviour
{
    private void Start()
    {
        if (RailwayDatabase.Instance == null)
        {
            Debug.LogError("Không tìm thấy RailwayDatabase!");
            return;
        }

        try
        {
            RunDatabaseTest();
        }
        catch (Exception ex)
        {
            Debug.LogError("DATABASE TEST FAILED: " + ex);
        }
    }

    private void RunDatabaseTest()
    {
        Debug.Log("========== DATABASE TEST START ==========");

        // ID riêng cho mỗi lần chạy để tránh trùng dữ liệu cũ
        string suffix = DateTime.Now.Ticks.ToString();

        string id1 = "TEST_" + suffix + "_001";
        string id2 = "TEST_" + suffix + "_002";
        string id3 = "TEST_" + suffix + "_003";

        // =====================================
        // CẤU TRÚC GIỐNG EXCEL - SHEET MESHES
        // =====================================

        var headers1 = new List<string>
        {
            "ID",
            "Loại cấu kiện",
            "Vật liệu",
            "Chiều dài (mm)",
            "Chiều rộng (mm)",
            "Chiều cao (mm)"
        };

        var types1 = new List<string>
        {
            "TEXT",
            "TEXT",
            "TEXT",
            "REAL",
            "REAL",
            "REAL"
        };

        // =====================================
        // TEST 1: IMPORT LẦN ĐẦU
        // =====================================

        var rows1 = new List<object[]>
        {
            new object[]
            {
                id1,
                "Tấm nền bê tông + Ray 49E1 (gộp 1 mesh)",
                "E)(S CONCRETE; Steel, Carbon",
                3000.0,
                2452.0,
                524.7
            },

            new object[]
            {
                id2,
                "Thanh ngang bê tông đúc sẵn",
                "Concrete, Precast; Laminate, red",
                2051.9,
                369.0,
                122.0
            }
        };

        RailwayDatabase.Instance.CreateDynamicTable(
            headers1,
            types1
        );

        RailwayDatabase.Instance.ImportDynamicData(
            headers1,
            types1,
            rows1,
            out int added1,
            out int updated1
        );

        Debug.Log("TEST 1 - IMPORT LẦN ĐẦU");
        Debug.Log("Thêm mới: " + added1);
        Debug.Log("Cập nhật: " + updated1);

        // =====================================
        // TEST 2: EXCEL CÓ THÊM CỘT MỚI
        // =====================================

        // Giả lập Excel được bổ sung cột Nhà sản xuất.
        // Đây không phải cột đang có trong file Excel hiện tại.

        var headers2 = new List<string>
        {
            "ID",
            "Loại cấu kiện",
            "Vật liệu",
            "Chiều dài (mm)",
            "Chiều rộng (mm)",
            "Chiều cao (mm)",
            "Nhà sản xuất"
        };

        var types2 = new List<string>
        {
            "TEXT",
            "TEXT",
            "TEXT",
            "REAL",
            "REAL",
            "REAL",
            "TEXT"
        };

        var rows2 = new List<object[]>
        {
            // Cập nhật cấu kiện đã tồn tại
            new object[]
            {
                id1,
                "Tấm nền bê tông + Ray 49E1 (đã cập nhật)",
                "E)(S CONCRETE; Steel, Carbon",
                3000.0,
                2452.0,
                524.7,
                "ABC"
            },

            // Thêm cấu kiện mới
            new object[]
            {
                id3,
                "Thanh ngang bê tông mới",
                "Concrete, Precast",
                2051.9,
                369.0,
                122.0,
                "XYZ"
            }
        };

        RailwayDatabase.Instance.CreateDynamicTable(
            headers2,
            types2
        );

        RailwayDatabase.Instance.ImportDynamicData(
            headers2,
            types2,
            rows2,
            out int added2,
            out int updated2
        );

        Debug.Log("TEST 2 - THÊM CỘT MỚI");
        Debug.Log("Thêm mới: " + added2);
        Debug.Log("Cập nhật: " + updated2);

        // =====================================
        // TEST 3: KIỂM TRA SCHEMA
        // =====================================

        var columns = RailwayDatabase.Instance.GetColumns();

        Debug.Log("TEST 3 - DANH SÁCH CỘT DATABASE");

        foreach (var column in columns)
        {
            Debug.Log(
                "Column: " + column.name +
                " | Type: " + column.type
            );
        }

        // Kiểm tra các cột bắt buộc có tồn tại
        foreach (string header in headers2)
        {
            bool exists = columns.Any(
                c => c.name == header
            );

            if (!exists)
            {
                throw new Exception(
                    "Database thiếu cột: " + header
                );
            }
        }

        Debug.Log("Kiểm tra schema: PASSED");

        // =====================================
        // TEST 4: KIỂM TRA KHÓA CHÍNH ID
        // =====================================

        var idColumn = columns.FirstOrDefault(
            c => c.name == "ID"
        );

        if (idColumn == null || idColumn.pk != 1)
        {
            throw new Exception(
                "ID chưa được thiết lập làm Primary Key!"
            );
        }

        Debug.Log("Kiểm tra Primary Key ID: PASSED");

        Debug.Log("========== DATABASE TEST FINISHED ==========");
    }
}
