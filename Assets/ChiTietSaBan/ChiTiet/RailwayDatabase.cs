using UnityEngine;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using SQLite;

public class RailwayDatabase : MonoBehaviour
{
    public Dictionary<string, string> GetElementDataByModelAndMesh(
    string modelID,
    string meshID)
{
    return GetElementData(modelID, meshID);
}


    public static event Action DatabaseChanged;
    public static RailwayDatabase Instance { get; private set; }

    private SQLiteConnection db;

    private const string TableName = "RailwayElements";

    // Database mới, không ghi đè Railway.db cũ
    private const string DatabaseFileName = "RailwayModels.db";

    // Các cột mặc định
    private static readonly List<string> DefaultHeaders = new List<string>
    {
        "ModelID",
        "MeshID",
        "Loại cấu kiện",
        "Vật liệu",
        "Chiều dài (mm)",
        "Chiều rộng (mm)",
        "Chiều cao (mm)"
    };

    private static readonly List<string> DefaultTypes = new List<string>
    {
        "TEXT",
        "TEXT",
        "TEXT",
        "TEXT",
        "REAL",
        "REAL",
        "REAL"
    };

    // =====================================================
    // 1. KHỞI TẠO DATABASE KHI UNITY CHẠY
    // =====================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        string path = Path.Combine(
            Application.persistentDataPath,
            DatabaseFileName
        );

        try
        {
            // SQLite tự tạo file nếu file chưa tồn tại
            db = new SQLiteConnection(path);

            // Tạo bảng và các cột mặc định
            EnsureTableExists();

            DontDestroyOnLoad(gameObject);

            Debug.Log("====================================");
            Debug.Log("RAILWAY DATABASE INITIALIZED");
            Debug.Log("Database path: " + path);
            Debug.Log("Table: " + TableName);
            Debug.Log("Primary key: ModelID + MeshID");
            Debug.Log("====================================");
        }
        catch (Exception ex)
        {
            Debug.LogError(
                "Database initialization failed: " + ex
            );

            db?.Close();
            db?.Dispose();
            db = null;

            Instance = null;
        }
    }

    // =====================================================
    // 2. TẠO BẢNG BAN ĐẦU
    // =====================================================

    private void EnsureTableExists()
    {
        if (db == null)
            throw new Exception("SQLite connection chưa được tạo!");

        string createSQL =
            "CREATE TABLE IF NOT EXISTS " + Quote(TableName) + " (" +

            Quote("ModelID") + " TEXT NOT NULL, " +
            Quote("MeshID") + " TEXT NOT NULL, " +
            Quote("Loại cấu kiện") + " TEXT, " +
            Quote("Vật liệu") + " TEXT, " +
            Quote("Chiều dài (mm)") + " REAL, " +
            Quote("Chiều rộng (mm)") + " REAL, " +
            Quote("Chiều cao (mm)") + " REAL, " +

            "PRIMARY KEY (" +
            Quote("ModelID") + ", " +
            Quote("MeshID") +
            "))";

        db.Execute(createSQL);

        // Đảm bảo bảng thực sự sử dụng khóa chính kép
        ValidateCompositePrimaryKey();

        // Nếu thiếu cột mặc định thì tự bổ sung
        EnsureColumns(DefaultHeaders, DefaultTypes);

        Debug.Log("Đã kiểm tra và khởi tạo bảng RailwayElements.");
    }

    // =====================================================
    // 3. KIỂM TRA KHÓA CHÍNH KÉP
    // =====================================================

    private void ValidateCompositePrimaryKey()
    {
        List<DynamicColumnBase> columns = GetColumns();

        var primaryKeys = columns
            .Where(c => c.pk > 0)
            .OrderBy(c => c.pk)
            .Select(c => c.name)
            .ToList();

        bool valid =
            primaryKeys.Count == 2 &&
            primaryKeys[0].Equals(
                "ModelID",
                StringComparison.OrdinalIgnoreCase
            ) &&
            primaryKeys[1].Equals(
                "MeshID",
                StringComparison.OrdinalIgnoreCase
            );

        if (!valid)
        {
            throw new Exception(
                "Cấu trúc khóa chính không hợp lệ! " +
                "Bảng " + TableName +
                " phải có khóa chính kép (ModelID, MeshID). " +
                "Có thể bạn đang sử dụng database cũ."
            );
        }
    }

    // =====================================================
    // 4. KIỂM TRA VÀ CHUẨN HÓA HEADER EXCEL
    // =====================================================

    private List<string> ValidateHeaders(
        List<string> headers,
        List<string> columnTypes)
    {
        if (headers == null || headers.Count == 0)
            throw new Exception("Excel không có tiêu đề cột!");

        if (columnTypes == null ||
            columnTypes.Count != headers.Count)
        {
            throw new Exception(
                "Số lượng columnTypes không khớp headers!"
            );
        }

        List<string> normalizedHeaders = headers
            .Select(h => h == null ? "" : h.Trim())
            .ToList();

        if (normalizedHeaders.Any(
            h => string.IsNullOrWhiteSpace(h)))
        {
            throw new Exception(
                "Excel có tiêu đề cột bị trống!"
            );
        }

        var duplicatedHeaders = normalizedHeaders
            .GroupBy(
                h => h,
                StringComparer.OrdinalIgnoreCase
            )
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicatedHeaders.Count > 0)
        {
            throw new Exception(
                "Excel có tên cột bị trùng: " +
                string.Join(", ", duplicatedHeaders)
            );
        }

        bool hasModelID = normalizedHeaders.Any(h =>
            h.Equals(
                "ModelID",
                StringComparison.OrdinalIgnoreCase
            )
        );

        bool hasMeshID = normalizedHeaders.Any(h =>
            h.Equals(
                "MeshID",
                StringComparison.OrdinalIgnoreCase
            )
        );

        if (!hasModelID || !hasMeshID)
        {
            throw new Exception(
                "Excel bắt buộc phải có cả hai cột ModelID và MeshID!"
            );
        }

        // Chuẩn hóa tên hai cột khóa chính
        for (int i = 0; i < normalizedHeaders.Count; i++)
        {
            if (normalizedHeaders[i].Equals(
                "ModelID",
                StringComparison.OrdinalIgnoreCase))
            {
                normalizedHeaders[i] = "ModelID";
            }

            if (normalizedHeaders[i].Equals(
                "MeshID",
                StringComparison.OrdinalIgnoreCase))
            {
                normalizedHeaders[i] = "MeshID";
            }
        }

        // Không sử dụng khóa ID cũ
        if (normalizedHeaders.Any(h =>
            h.Equals("ID", StringComparison.OrdinalIgnoreCase)))
        {
            throw new Exception(
                "Không sử dụng cột ID cũ nữa. " +
                "Hãy dùng ModelID và MeshID làm khóa chính."
            );
        }

        return normalizedHeaders;
    }

    // =====================================================
    // 5. KIỂM TRA KIỂU DỮ LIỆU SQL
    // =====================================================

    private string ValidateColumnType(string type)
    {
        if (string.IsNullOrWhiteSpace(type))
            return "TEXT";

        string normalizedType = type.Trim().ToUpperInvariant();

        switch (normalizedType)
        {
            case "TEXT":
            case "REAL":
            case "INTEGER":
            case "NUMERIC":
            case "BLOB":
                return normalizedType;

            default:
                throw new Exception(
                    "Kiểu dữ liệu SQL không được hỗ trợ: " + type
                );
        }
    }

    // =====================================================
    // 6. TỰ ĐỘNG THÊM CỘT MỚI
    // =====================================================

    private void EnsureColumns(
        List<string> headers,
        List<string> columnTypes)
    {
        if (db == null)
            throw new Exception("Database chưa được khởi tạo!");

        if (headers == null || columnTypes == null ||
            headers.Count != columnTypes.Count)
        {
            throw new Exception(
                "Danh sách headers và columnTypes không hợp lệ!"
            );
        }

        List<DynamicColumnBase> existingColumns = GetColumns();

        var existingNames = new HashSet<string>(
            existingColumns.Select(c => c.name),
            StringComparer.OrdinalIgnoreCase
        );

        List<string> addedColumns = new List<string>();

        db.RunInTransaction(() =>
        {
            for (int i = 0; i < headers.Count; i++)
            {
                string column = headers[i].Trim();

                // Hai cột khóa chính đã được tạo khi khởi tạo bảng
                if (column.Equals(
                    "ModelID",
                    StringComparison.OrdinalIgnoreCase) ||
                    column.Equals(
                    "MeshID",
                    StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (existingNames.Contains(column))
                    continue;

                string type = ValidateColumnType(columnTypes[i]);

                string addSQL =
                    "ALTER TABLE " + Quote(TableName) +
                    " ADD COLUMN " + Quote(column) +
                    " " + type;

                db.Execute(addSQL);

                addedColumns.Add(column);
                existingNames.Add(column);
            }
        });

        if (addedColumns.Count > 0)
        {
            Debug.Log(
                "Đã tự động thêm cột mới: " +
                string.Join(", ", addedColumns)
            );
        }
    }

    // =====================================================
    // 7. ĐỒNG BỘ CẤU TRÚC DATABASE
    // =====================================================

    public void CreateDynamicTable(
        List<string> headers,
        List<string> columnTypes)
    {
        if (db == null)
            throw new Exception("Database chưa được khởi tạo!");

        List<string> normalizedHeaders =
            ValidateHeaders(headers, columnTypes);

        // Kiểm tra bảng và khóa chính
        EnsureTableExists();

        // Chỉ thêm cột mới, không xóa cột cũ
        EnsureColumns(normalizedHeaders, columnTypes);

        Debug.Log("Đồng bộ cấu trúc database thành công.");
    }

    // =====================================================
    // 8. IMPORT HOẶC CẬP NHẬT DỮ LIỆU EXCEL
    // =====================================================

    public void ImportDynamicData(
        List<string> headers,
        List<string> columnTypes,
        List<object[]> rows,
        out int added,
        out int updated)
    {
        added = 0;
        updated = 0;

        if (db == null)
            throw new Exception("Database chưa được khởi tạo!");

        if (rows == null)
            throw new Exception("Danh sách dữ liệu Excel bị null!");

        List<string> normalizedHeaders =
            ValidateHeaders(headers, columnTypes);

        // Đồng bộ schema trước khi import
        CreateDynamicTable(normalizedHeaders, columnTypes);

        int modelIndex = normalizedHeaders.FindIndex(h =>
            h.Equals(
                "ModelID",
                StringComparison.OrdinalIgnoreCase
            )
        );

        int meshIndex = normalizedHeaders.FindIndex(h =>
            h.Equals(
                "MeshID",
                StringComparison.OrdinalIgnoreCase
            )
        );

        // Chuẩn bị dữ liệu và kiểm tra khóa chính
        List<object[]> normalizedRows = new List<object[]>();

        foreach (object[] row in rows)
        {
            if (row == null ||
                row.Length != normalizedHeaders.Count)
            {
                throw new Exception(
                    "Một dòng Excel có số lượng dữ liệu " +
                    "không khớp số cột!"
                );
            }

            object[] newRow = (object[])row.Clone();

            string modelID =
                Convert.ToString(newRow[modelIndex])?.Trim();

            string meshID =
                Convert.ToString(newRow[meshIndex])?.Trim();

            if (string.IsNullOrWhiteSpace(modelID))
            {
                throw new Exception(
                    "Excel có bản ghi bị thiếu ModelID!"
                );
            }

            if (string.IsNullOrWhiteSpace(meshID))
            {
                throw new Exception(
                    "Excel có bản ghi bị thiếu MeshID!"
                );
            }

            newRow[modelIndex] = modelID;
            newRow[meshIndex] = meshID;

            normalizedRows.Add(newRow);
        }

        // Tạo danh sách tên cột
        string columnNames = string.Join(
            ", ",
            normalizedHeaders.Select(h => Quote(h))
        );

        // Tạo dấu ? tương ứng với số cột
        string parameters = string.Join(
            ", ",
            normalizedHeaders.Select(h => "?")
        );

        // Các cột được cập nhật khi khóa chính đã tồn tại
        var updateColumns = normalizedHeaders
            .Where(h =>
                !h.Equals(
                    "ModelID",
                    StringComparison.OrdinalIgnoreCase
                ) &&
                !h.Equals(
                    "MeshID",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            .ToList();

        string conflictAction;

        if (updateColumns.Count > 0)
        {
            conflictAction =
                "DO UPDATE SET " +
                string.Join(
                    ", ",
                    updateColumns.Select(h =>
                        Quote(h) +
                        " = excluded." +
                        Quote(h)
                    )
                );
        }
        else
        {
            conflictAction = "DO NOTHING";
        }

        // Khóa chính kép
        string sql =
            "INSERT INTO " + Quote(TableName) +
            " (" + columnNames + ")" +
            " VALUES (" + parameters + ")" +
            " ON CONFLICT(" +
            Quote("ModelID") + ", " +
            Quote("MeshID") +
            ") " + conflictAction;

        int newCount = 0;
        int updateCount = 0;

        db.RunInTransaction(() =>
        {
            foreach (object[] row in normalizedRows)
            {
                string modelID =
                    Convert.ToString(row[modelIndex]);

                string meshID =
                    Convert.ToString(row[meshIndex]);

                int count = db.ExecuteScalar<int>(
                    "SELECT COUNT(*) FROM " + Quote(TableName) +
                    " WHERE " + Quote("ModelID") + " = ?" +
                    " AND " + Quote("MeshID") + " = ?",
                    modelID,
                    meshID
                );

                if (count == 0)
                    newCount++;
                else
                    updateCount++;

                // Thêm mới hoặc cập nhật dữ liệu
                db.Execute(sql, row);
            }
        });

        added = newCount;
        updated = updateCount;

        Debug.Log("========== IMPORT COMPLETED ==========");
        Debug.Log("New records: " + added);
        Debug.Log("Updated records: " + updated);
        Debug.Log("Total imported: " + normalizedRows.Count);
        Debug.Log("======================================");

        if (added > 0 || updated > 0)
        {
            DatabaseChanged?.Invoke();
        }
    }

    // =====================================================
    // 9. LẤY THÔNG TIN THEO MODELID + MESHID
    // =====================================================

    public Dictionary<string, string> GetElementData(
        string modelID,
        string meshID)
    {
        var result = new Dictionary<string, string>();

        if (db == null ||
            string.IsNullOrWhiteSpace(modelID) ||
            string.IsNullOrWhiteSpace(meshID))
        {
            return result;
        }

        modelID = modelID.Trim();
        meshID = meshID.Trim();

        int count = db.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM " + Quote(TableName) +
            " WHERE " + Quote("ModelID") + " = ?" +
            " AND " + Quote("MeshID") + " = ?",
            modelID,
            meshID
        );

        if (count == 0)
            return result;

        List<DynamicColumnBase> columns = GetColumns();

        foreach (var column in columns)
        {
            string sql =
                "SELECT CAST(" + Quote(column.name) + " AS TEXT)" +
                " FROM " + Quote(TableName) +
                " WHERE " + Quote("ModelID") + " = ?" +
                " AND " + Quote("MeshID") + " = ?" +
                " LIMIT 1";

            string value = db.ExecuteScalar<string>(
                sql,
                modelID,
                meshID
            );

            result[column.name] = value ?? "";
        }

        return result;
    }

    // =====================================================
    // 10. LẤY THÔNG TIN TỔNG THỂ CỦA MODEL
    // MeshID = ModelID
    // =====================================================

    public Dictionary<string, string> GetModelData(string modelID)
    {
        if (string.IsNullOrWhiteSpace(modelID))
            return new Dictionary<string, string>();

        return GetElementData(modelID, modelID);
    }

    // =====================================================
    // 11. LẤY THÔNG TIN MESH THUỘC MODEL
    // =====================================================

    public Dictionary<string, string> GetMeshData(
        string modelID,
        string meshID)
    {
        return GetElementData(modelID, meshID);
    }

    // =====================================================
    // 12. TƯƠNG THÍCH VỚI CODE CŨ
    // =====================================================

    [Obsolete(
        "Hãy dùng GetModelData(modelID) hoặc " +
        "GetElementData(modelID, meshID)."
    )]
    public Dictionary<string, string> GetElementDataById(string id)
    {
        // Hàm cũ chỉ còn tương thích với thông tin tổng thể model
        return GetModelData(id);
    }

    // =====================================================
    // 13. LẤY DANH SÁCH CỘT TRONG DATABASE
    // =====================================================

    public List<DynamicColumnBase> GetColumns()
    {
        if (db == null)
        {
            throw new Exception(
                "Database chưa được khởi tạo!"
            );
        }

        return db.Query<DynamicColumnBase>(
            "PRAGMA table_info(" + Quote(TableName) + ")"
        );
    }

    // =====================================================
    // 14. XÓA CÁC CỘT THỬ NGHIỆM RỖNG
    // Chỉ xóa khi người dùng chủ động gọi Context Menu
    // =====================================================

    [ContextMenu("Xoa cac cot thu nghiem rong")]
    public void RemoveEmptyColumns()
    {
        if (db == null)
        {
            Debug.LogError(
                "Database chưa khởi tạo! Hãy chạy Play Mode trước."
            );
            return;
        }

        try
        {
            int rowCount = db.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM " + Quote(TableName)
            );

            if (rowCount == 0)
            {
                Debug.LogWarning(
                    "Database chưa có bản ghi. Không xóa cột nào."
                );
                return;
            }

            List<DynamicColumnBase> columns = GetColumns();

            // Không xóa các cột khóa chính và cột mặc định
            var protectedColumns = new HashSet<string>(
                DefaultHeaders,
                StringComparer.OrdinalIgnoreCase
            );

            List<string> emptyColumns = new List<string>();

            foreach (var column in columns)
            {
                if (protectedColumns.Contains(column.name))
                    continue;

                string sql =
                    "SELECT COUNT(*) FROM " + Quote(TableName) +
                    " WHERE " + Quote(column.name) + " IS NOT NULL" +
                    " AND TRIM(CAST(" + Quote(column.name) +
                    " AS TEXT)) <> ''";

                int filledCount = db.ExecuteScalar<int>(sql);

                if (filledCount == 0)
                {
                    emptyColumns.Add(column.name);
                }
            }

            if (emptyColumns.Count == 0)
            {
                Debug.Log(
                    "Không tìm thấy cột thử nghiệm rỗng."
                );
                return;
            }

            Debug.Log(
                "Các cột sẽ bị xóa: " +
                string.Join(", ", emptyColumns)
            );

            string versionText = db.ExecuteScalar<string>(
                "SELECT sqlite_version()"
            );

            Version sqliteVersion;

            if (!Version.TryParse(versionText, out sqliteVersion) ||
                sqliteVersion < new Version(3, 35, 0))
            {
                Debug.LogError(
                    "SQLite phiên bản " + versionText +
                    " không hỗ trợ DROP COLUMN."
                );
                return;
            }

            db.RunInTransaction(() =>
            {
                foreach (string column in emptyColumns)
                {
                    db.Execute(
                        "ALTER TABLE " + Quote(TableName) +
                        " DROP COLUMN " + Quote(column)
                    );
                }
            });

            Debug.Log(
                "Đã xóa " + emptyColumns.Count +
                " cột thử nghiệm rỗng."
            );

            DatabaseChanged?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.LogError(
                "Không thể xóa cột rỗng: " + ex
            );
        }
    }

    // =====================================================
    // 15. QUOTE TÊN BẢNG VÀ TÊN CỘT
    // =====================================================

    private static string Quote(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tên cột hoặc bảng bị trống!");

        return "\"" + name.Replace("\"", "\"\"") + "\"";
    }

    // =====================================================
    // 16. ĐÓNG DATABASE
    // =====================================================

    private void OnDestroy()
    {
        if (Instance == this)
        {
            db?.Close();
            db?.Dispose();

            db = null;
            Instance = null;
        }
    }
}

// =========================================================
// 17. CLASS ĐỌC THÔNG TIN CỘT SQLITE
// =========================================================

public class DynamicColumnBase
{
    public int cid { get; set; }

    public string name { get; set; }

    public string type { get; set; }

    public int notnull { get; set; }

    public string dflt_value { get; set; }

    public int pk { get; set; }
}