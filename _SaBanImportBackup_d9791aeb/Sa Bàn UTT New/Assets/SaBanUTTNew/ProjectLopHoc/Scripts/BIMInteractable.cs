using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class BIMInteractable : InteractableObject
{
    private void Awake()
    {
        targetSceneName = string.Empty;
        highlightRenderer = null;
        objectName = gameObject.name;
    }

    public override void OnInteract()
    {
        targetSceneName = string.Empty;

        Dictionary<string, string> danhSachKyThuat = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string tenCauKien = gameObject.name;

        // Quét tất cả các component Metadata trong toàn bộ cụm mô hình
        TimDuLieuKyThuatToiUu(gameObject, danhSachKyThuat, ref tenCauKien);

        string noiDungBang = DinhDangBangDuLieu(danhSachKyThuat);

        if (TrainInfoPopupUI.Instance != null)
        {
            TrainInfoPopupUI.Instance.OpenPopup(tenCauKien, noiDungBang);
        }
    }

    private void TimDuLieuKyThuatToiUu(GameObject goc, Dictionary<string, string> ketQuaToiUu, ref string tenHienThi)
    {
        // Thu thập component Metadata từ cả đối tượng hiện tại, toàn bộ con và cha
        List<Component> danhSachMeta = new List<Component>();
        
        Component[] compsCon = goc.GetComponentsInChildren<Component>(true);
        foreach (var c in compsCon)
        {
            if (c != null && LaComponentMetadata(c.GetType().Name) && !danhSachMeta.Contains(c))
                danhSachMeta.Add(c);
        }

        Component[] compsCha = goc.GetComponentsInParent<Component>(true);
        foreach (var c in compsCha)
        {
            if (c != null && LaComponentMetadata(c.GetType().Name) && !danhSachMeta.Contains(c))
                danhSachMeta.Add(c);
        }

        int soThuocTinhNhieuNhat = -1;
        Dictionary<string, string> boTam = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Duyệt qua từng Metadata tìm được và chọn bảng có nhiều thông số kỹ thuật thực tế nhất
        foreach (Component metaComp in danhSachMeta)
        {
            boTam.Clear();
            TrichXuatMetadata(metaComp, boTam);

            int diemKyThuat = TinhDiemThongSoKyThuat(boTam);
            if (diemKyThuat > soThuocTinhNhieuNhat)
            {
                soThuocTinhNhieuNhat = diemKyThuat;
                ketQuaToiUu.Clear();
                foreach (var kvp in boTam)
                {
                    ketQuaToiUu[kvp.Key] = kvp.Value;
                }
                tenHienThi = LamSachTenCauKien(metaComp.gameObject.name);
            }
        }

        Debug.Log($"[BIMInteractable] Đã quét {danhSachMeta.Count} component Metadata. Cấu kiện '{tenHienThi}' trích xuất được {ketQuaToiUu.Count} trường hợp lệ.");
    }

    private bool LaComponentMetadata(string typeName)
    {
        return typeName.IndexOf("Metadata", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private int TinhDiemThongSoKyThuat(Dictionary<string, string> dict)
    {
        if (dict.Count == 0) return -1;
        int diem = dict.Count;

        // Ưu tiên cao hơn nếu bảng chứa các trường kích thước hoặc nhà sản xuất thực tế
        foreach (var key in dict.Keys)
        {
            string k = key.ToLowerInvariant();
            if (k.Contains("manufacturer") || k.Contains("length") || k.Contains("height") || 
                k.Contains("model") || k.Contains("category") || k.Contains("material"))
            {
                diem += 10;
            }
        }
        return diem;
    }

    private void TrichXuatMetadata(Component comp, Dictionary<string, string> ketQua)
    {
        Type kieu = comp.GetType();
        BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        // 1. Quét biến và thuộc tính
        while (kieu != null && kieu != typeof(MonoBehaviour) && kieu != typeof(Component))
        {
            FieldInfo[] fields = kieu.GetFields(flags);
            foreach (FieldInfo f in fields)
            {
                object val = null;
                try { val = f.GetValue(comp); } catch { }
                if (val != null) XuLyTapDuLieu(val, ketQua);
            }

            PropertyInfo[] props = kieu.GetProperties(flags);
            foreach (PropertyInfo p in props)
            {
                if (p.GetIndexParameters().Length > 0 || !p.CanRead) continue;
                object val = null;
                try { val = p.GetValue(comp, null); } catch { }
                if (val != null) XuLyTapDuLieu(val, ketQua);
            }

            // 2. Thử gọi các hàm lấy danh sách nếu có
            MethodInfo[] methods = kieu.GetMethods(flags);
            foreach (MethodInfo m in methods)
            {
                if (m.GetParameters().Length != 0 || m.ReturnType == typeof(void)) continue;
                string mName = m.Name.ToLowerInvariant();
                if (mName.Contains("propert") || mName.Contains("meta") || mName.Contains("entr"))
                {
                    object val = null;
                    try { val = m.Invoke(comp, null); } catch { }
                    if (val != null) XuLyTapDuLieu(val, ketQua);
                }
            }

            kieu = kieu.BaseType;
        }
    }

    private void XuLyTapDuLieu(object data, Dictionary<string, string> ketQua)
    {
        if (data is IDictionary dict)
        {
            foreach (DictionaryEntry de in dict)
            {
                ThemThuocTinh(de.Key?.ToString(), de.Value?.ToString(), ketQua);
            }
            return;
        }

        if (data is IEnumerable list && !(data is string) && !(data is Component) && !(data is GameObject))
        {
            foreach (object item in list)
            {
                if (item == null) continue;
                DocCapNameValue(item, ketQua);
            }
        }
    }

    private void DocCapNameValue(object item, Dictionary<string, string> ketQua)
    {
        Type t = item.GetType();
        BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        string ten = null;
        string giaTri = null;

        // Quét cả Field và Property bên trong từng phần tử struct của PiXYZ
        FieldInfo[] fields = t.GetFields(flags);
        foreach (var f in fields)
        {
            string fName = f.Name.TrimStart('_', 'm', '<').ToLowerInvariant();
            if (ten == null && (fName.StartsWith("name") || fName.StartsWith("key")))
                ten = LayGiaTriChuoi(f.GetValue(item));
            else if (giaTri == null && (fName.StartsWith("val") || fName.StartsWith("value")))
                giaTri = LayGiaTriChuoi(f.GetValue(item));
        }

        PropertyInfo[] props = t.GetProperties(flags);
        foreach (var p in props)
        {
            if (!p.CanRead || p.GetIndexParameters().Length > 0) continue;
            string pName = p.Name.TrimStart('_', 'm').ToLowerInvariant();
            if (ten == null && (pName.StartsWith("name") || pName.StartsWith("key")))
            {
                try { ten = LayGiaTriChuoi(p.GetValue(item, null)); } catch { }
            }
            else if (giaTri == null && (pName.StartsWith("val") || pName.StartsWith("value")))
            {
                try { giaTri = LayGiaTriChuoi(p.GetValue(item, null)); } catch { }
            }
        }

        ThemThuocTinh(ten, giaTri, ketQua);
    }

    private void ThemThuocTinh(string pName, string pVal, Dictionary<string, string> ketQua)
    {
        if (string.IsNullOrWhiteSpace(pName) || string.IsNullOrWhiteSpace(pVal)) return;

        string upper = pName.ToUpperInvariant();

        // Loại bỏ mã hệ thống nội bộ của Revit và IFC
        if (upper.StartsWith("OWNERHISTORY") || 
            upper.StartsWith("IFCGEOMETRIC") || 
            upper.Contains("GLOBALID") || 
            upper.Contains("CHANGEACTION") || 
            upper.Contains("CREATIONDATE") || 
            upper.Contains("APPLICATION") || 
            upper.Contains("USER") || 
            upper.Equals("TYPE") || 
            upper.Contains("INTERNAL") || 
            upper.StartsWith("__"))
        {
            return;
        }

        // Cắt gọn tiền tố thư mục dài để giữ lại tên thuộc tính chính xác
        string tenRutGon = pName;
        int viTriGach = pName.LastIndexOf('/');
        if (viTriGach >= 0 && viTriGach < pName.Length - 1)
        {
            tenRutGon = pName.Substring(viTriGach + 1);
        }

        tenRutGon = tenRutGon.Trim();
        if (!ketQua.ContainsKey(tenRutGon))
        {
            ketQua.Add(tenRutGon, pVal.Trim());
        }
    }

    private string LayGiaTriChuoi(object obj)
    {
        if (obj == null) return null;
        if (obj is string s) return s;
        if (obj.GetType().IsPrimitive) return obj.ToString();

        BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        FieldInfo vf = obj.GetType().GetField("value", flags) ?? obj.GetType().GetField("_value", flags);
        if (vf != null)
        {
            object v = vf.GetValue(obj);
            if (v != null) return v.ToString();
        }

        PropertyInfo vp = obj.GetType().GetProperty("Value", flags) ?? obj.GetType().GetProperty("value", flags);
        if (vp != null && vp.CanRead)
        {
            try
            {
                object v = vp.GetValue(obj, null);
                if (v != null) return v.ToString();
            }
            catch { }
        }

        return obj.ToString();
    }

    private string LamSachTenCauKien(string rawName)
    {
        // Rút gọn bớt chuỗi ID số phía sau nếu tên quá dài
        int viTriHaiCham = rawName.LastIndexOf(':');
        if (viTriHaiCham > 0 && viTriHaiCham < rawName.Length - 1)
        {
            return rawName.Substring(0, viTriHaiCham);
        }
        return rawName;
    }

    private string DinhDangBangDuLieu(Dictionary<string, string> danhSach)
    {
        if (danhSach.Count == 0)
        {
            return "<align=\"center\"><color=#FFA500>Không đọc được trường thông số kỹ thuật.</color></align>";
        }

        StringBuilder sb = new StringBuilder();
        foreach (var pair in danhSach)
        {
            string tenHienThi = pair.Key.Length > 26 ? pair.Key.Substring(0, 24) + ".." : pair.Key;

            sb.Append($"<align=\"left\"><b>{tenHienThi}</b></align><line-height=0>\n");
            sb.Append($"<align=\"right\"><color=#F1F5F9>{pair.Value}</color></align><line-height=1.25em>\n");
        }
        return sb.ToString();
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(BIMInteractable))]
public class BIMInteractableEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox(
            "Trình Quét Metadata BIM Tự Động:\n" +
            "• Quét đa tầng, tự động chọn bảng dữ liệu chi tiết nhất.\n" +
            "• Đọc được cả field và property của PiXYZ.\n" +
            "• Tên thuộc tính lề trái, giá trị thuộc tính lề phải.", 
            MessageType.Info);
    }
}
#endif