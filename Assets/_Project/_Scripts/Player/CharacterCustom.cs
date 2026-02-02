using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using Firebase.Database;
using PhotonTable = ExitGames.Client.Photon.Hashtable;
using Firebase;


/// <summary>
/// 커스터마이징 가능한 아이템 카테고리 정의
/// </summary>
#region enum Item Category
public enum ItemCategory { Head, Body, Shoes }
#endregion

/// <summary>
/// 하나의 아이템 옵션을 표현하는 데이터 구조.
/// - 하나의 옵션이 여러 GameObject(헬멧 + 상의 + 하의 등)를 포함할 수 있음
/// - id는 Firebase / Photon 동기화를 위한 고유 키
/// </summary>
#region class Item Option
[Serializable]
public class ItemOption
{
    [Tooltip("외부 저장/네트워크 동기화를 위한 고유 ID (공백 X)")]
    public string id;

    [Tooltip("파츠 오브젝트")]
    public GameObject[] parts;

    [Header("Colour Option")]
    [Tooltip("URP Lit이면 _BaseColor")]
    public string colorProperty = "_BaseColor";
}
#endregion

/// <summary>
/// 카테고리별 아이템 묶음
/// - 현재 선택된 인덱스와 색상 상태를 함께 관리
/// </summary>
#region class Item Category Set
[Serializable]
public class ItemCategorySet
{
    public ItemCategory category;
    public ItemOption[] options;

    [HideInInspector] public int currentIndex = -1; // -1이면 아무것도 선택 안 함
    [HideInInspector] public Color currentColor = Color.white;
}
#endregion

/// <summary>
/// 캐릭터 외형 커스터마이징을 담당하는 핵심 클래스.
/// - 로컬 미리보기
/// - Firebase 저장
/// - Photon Custom Properties 변환
/// - 네트워크 인스턴스/로컬 인스턴스 모두 대응
/// </summary>
#region  class Character Custom
public class CharacterCustom : MonoBehaviourPunCallbacks
{
    [Header("Categories")]
    public ItemCategorySet[] categories;

    public GameObject stickmanBody;

    // Inner cache
    private Dictionary<string, GameObject> nameToGO;
    private List<GameObject> tempList = new List<GameObject>();
    private MaterialPropertyBlock mpb;

    // Firebase
    private DatabaseReference DB => FirebaseManager.Instance.DB.RootReference;
    private DatabaseReference CustomizeDataRef => FirebaseManager.Instance.CurrentUserCustomizationDataRef;

    private HashSet<GameObject> toggleables;

    // Initialize
    void Awake()
    {
        mpb = new MaterialPropertyBlock();
        BuildNameLookup();

        if (stickmanBody) stickmanBody.SetActive(true);

        ToggleablesList();
        SetAllOff();
    }

    void BuildNameLookup()
    {
        nameToGO = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        var all = GetComponentsInChildren<Transform>(true);
        foreach (var t in all)
        {
            // 마지막 거 덮어쓰기 안 되게
            if (!nameToGO.ContainsKey(t.name))
                nameToGO.Add(t.name, t.gameObject);
        }
    }

    void Start()
    {
        // PhotonView 존재: 실제 게임 플레이 캐릭터
        // PhotonView 없음: 로컬 커스터마이징 미리보기
        if (TryGetComponent<PhotonView>(out PhotonView view))
        {
            ApplyData(PhotonManager.Instance.customizationDict[view.Owner.UserId]);
        }
        else
        {
            ApplyData(CustomizationData.Local);
        }
    }

    // Stickman제외 모두 off
    //(모든 커스터마이징 가능한 파츠 GameObject를 수집)
    private void ToggleablesList()
    {
        toggleables = new HashSet<GameObject>();
        if (categories == null) return;
        foreach (var set in categories)
        {
            if (set?.options == null) continue;
            foreach (var opt in set.options)
            {
                if (opt?.parts == null) continue;
                foreach (var go in opt.parts)
                    if (go) toggleables.Add(go);
            }
        }
    }

    // 모든 파츠를 끄고 기본 바디 상태로 초기화
    void SetAllOff()
    {
        if (toggleables == null) ToggleablesList();
        foreach (var go in toggleables)
        {
            if (go) go.SetActive(false);
        }

        if (stickmanBody) stickmanBody.SetActive(true);

        foreach (var cat in categories)
        {
            cat.currentIndex = -1;
            cat.currentColor = Color.white;
        }
    }

    // Prev / Next UI
    public void Next(ItemCategory cat)
    {
        var set = GetSet(cat);
        if (set == null || set.options == null || set.options.Length == 0) return;

        int next = set.currentIndex + 1;
        if (next >= set.options.Length) next = -1; // 넘어가면 '해제' 상태

        set.currentColor = Color.white;
        ApplySelection(cat, next);
    }

    public void Prev(ItemCategory cat)
    {
        var set = GetSet(cat);
        if (set == null || set.options == null || set.options.Length == 0) return;

        int prev = set.currentIndex - 1;
        if (prev < -1) prev = set.options.Length - 1;
        set.currentColor = Color.white;
        ApplySelection(cat, prev);
    }

    ItemCategorySet GetSet(ItemCategory cat)
    {
        foreach (var s in categories) if (s.category == cat) return s;
        return null;
    }

    // 특정 카테고리에 대한 아이템 선택 적용
    public void ApplySelection(ItemCategory cat, int index)
    {
        var set = GetSet(cat);
        if (set == null) return;

        // 이전 선택 해제
        if (set.currentIndex >= 0 && set.currentIndex < set.options.Length)
            ToggleItem(set.options[set.currentIndex], false);

        set.currentIndex = index;

        // 새 선택 적용
        if (index >= 0 && index < set.options.Length)
        {
            ToggleItem(set.options[index], true);
            ApplyColor(cat, set.currentColor, set.options[index]);
        }

        if (stickmanBody) stickmanBody.SetActive(true);
    }

    bool AllCategoriesOff()
    {
        foreach (var c in categories) if (c.currentIndex >= 0) return false;
        return true;
    }

    void ToggleItem(ItemOption opt, bool on)
    {
        if (opt?.parts == null) return;

        foreach (var go in opt.parts)
        {
            if (!go) continue;
            go.SetActive(on);
        }
    }

    // Color Settings
    // 대표색만 / 전체색, 둘 다 지원. UI에서 컬러피커 값 들어오면 호출.
    private Dictionary<Renderer, Color[]> originalColors = new Dictionary<Renderer, Color[]>();

    void CacheOriginalColors(ItemOption opt, string prop)
    {
        if (opt == null || opt.parts == null) return;

        foreach (var go in opt.parts)
        {
            if (!go) continue;

            var rends = go.GetComponentsInChildren<Renderer>(true);
            foreach (var r in rends)
            {
                if (!originalColors.ContainsKey(r))
                {
                    var mats = r.sharedMaterials;
                    Color[] colors = new Color[mats.Length];
                    for (int i = 0; i < mats.Length; i++)
                    {
                        colors[i] = mats[i].GetColor(prop);
                    }
                    originalColors[r] = colors; // 원본 저장
                }
            }
        }
    }

    public void SetColor(ItemCategory cat, Color color, bool overrideAllMats = false)
    {
        var set = GetSet(cat);
        if (set == null) return;

        set.currentColor = color;

        if (set.currentIndex >= 0 && set.currentIndex < set.options.Length)
        {
            var opt = set.options[set.currentIndex];
            ApplyColor(cat, color, opt);
        }
    }

    // 현재 선택된 아이템에 색상 적용
    // (원본 색상 * 선택 색상 방식)
    void ApplyColor(ItemCategory cat, Color color, ItemOption opt)
    {
        if (opt == null || opt.parts == null) return;

        foreach (var go in opt.parts)
        {
            if (!go || !go.activeInHierarchy) continue;

            var rends = go.GetComponentsInChildren<Renderer>(true);
            foreach (var r in rends)
            {
                string prop =
                    HasColor(r, opt.colorProperty) ? opt.colorProperty :
                    (HasColor(r, "_BaseColor") ? "_BaseColor" :
                    (HasColor(r, "_Color") ? "_Color" : null));

                if (prop == null) continue;

                // 원래 색상 캐싱
                CacheOriginalColors(opt, prop);

                // 원래 색상 가져오기
                if (!originalColors.ContainsKey(r)) continue;
                Color[] originals = originalColors[r];
                var mats = r.materials;
                for (int i = 0; i < mats.Length; i++)
                {
                    Color baseColor = originals[i];  // 항상 원본 기준
                    Color multiplied = baseColor * color;
                    mats[i].SetColor(prop, multiplied);
                }
            }
        }
    }

    bool HasColor(Renderer r, string prop)
    {
        if (string.IsNullOrEmpty(prop)) return false;
        var mats = r.sharedMaterials;
        if (mats == null) return false;
        foreach (var m in mats)
        {
            if (!m) continue;
            if (m.HasProperty(prop)) return true;
        }
        return false;
    }

    public void ResetCustomization()
    {
        if (CustomizationData.Local != null)
        {
            ApplyData(CustomizationData.Local);
        }

        
        else
        {
            foreach (var kv in originalColors)
            {
                Renderer r = kv.Key;
                Color[] origs = kv.Value;

                var mats = r.materials;
                for (int i = 0; i < mats.Length && i < origs.Length; i++)
                {
                    if (mats[i] != null && mats[i].HasProperty("_BaseColor"))
                        mats[i].SetColor("_BaseColor", origs[i]);
                }
            }

            foreach (var cat in categories)
            {
                if (cat.currentIndex >= 0 && cat.currentIndex < cat.options.Length)
                    ToggleItem(cat.options[cat.currentIndex], false);

                cat.currentIndex = -1;
                cat.currentColor = Color.white;
            }
        }

        if (stickmanBody) stickmanBody.SetActive(true);
    }
    public void ResetColors()
    {
        foreach (var kv in originalColors)
        {
            Renderer r = kv.Key;
            Color[] originals = kv.Value;
            var mats = r.materials;
            for (int i = 0; i < mats.Length; i++)
            {
                mats[i].SetColor("_BaseColor", originals[i]);
            }
        }

        // UI 상태도 초기화
        foreach (var cat in categories)
        {
            cat.currentColor = Color.white;
        }
    }

    // Firebase Realtime DB (Save/Load)
    public async Task SaveToFirebase()
    {
        try
        {
            var data = BuildData();
            string json = JsonUtility.ToJson(data);
            await CustomizeDataRef.SetRawJsonValueAsync(json);

            // 저장 성공 시 로컬 데이터 갱신
            CustomizationData.Local = data;
        } 
        catch (FirebaseException fe)
        {
            Debug.Log(fe.Message);
        }
    }

    // PUN2 동기화
    void BroadcastToPhoton()
    {
        if (!PhotonNetwork.InRoom) return;
        PhotonNetwork.LocalPlayer.SetCustomProperties(BuildData().ToPhoton());
    }

    // 직렬화/역직렬화
    CustomizationData BuildData()
    {
        var d = new CustomizationData();
        foreach (var set in categories)
        {
            switch (set.category)
            {
                case ItemCategory.Head:
                    d.headId = IdOf(set);
                    d.headColor = ColorUtil.ToHex(set.currentColor);
                    d.headAll = CurrentRecolorAll(set);
                    break;
                case ItemCategory.Body:
                    d.bodyId = IdOf(set);
                    d.bodyColor = ColorUtil.ToHex(set.currentColor);
                    d.bodyAll = CurrentRecolorAll(set);
                    break;
                case ItemCategory.Shoes:
                    d.shoesId = IdOf(set);
                    d.shoesColor = ColorUtil.ToHex(set.currentColor);
                    d.shoesAll = CurrentRecolorAll(set);
                    break;
            }
        }
        return d;
    }

    string IdOf(ItemCategorySet set)
    {
        if (set.currentIndex < 0 || set.options == null || set.options.Length == 0) return "";
        var opt = set.options[set.currentIndex];
        return string.IsNullOrEmpty(opt.id) ? "" : opt.id;
    }

    bool CurrentRecolorAll(ItemCategorySet set)
    {
        return true;
    }

    // 커스터마이징 데이터를 실제 캐릭터에 적용
    void ApplyData(CustomizationData d)
    {
        // 전부 끄고 다시 선택
        foreach (var c in categories) ApplySelection(c.category, -1);

        ApplyOne(ItemCategory.Head, d.headId, d.headColor, d.headAll);
        ApplyOne(ItemCategory.Body, d.bodyId, d.bodyColor, d.bodyAll);
        ApplyOne(ItemCategory.Shoes, d.shoesId, d.shoesColor, d.shoesAll);

        if (stickmanBody) stickmanBody.SetActive(true);
    }

    void ApplyOne(ItemCategory cat, string id, string hexColor, bool all)
    {
        var set = GetSet(cat);
        if (set == null) return;

        int idx = IndexOfId(set, id);
        ApplySelection(cat, idx);

        var color = ColorUtil.FromHexOr(hexColor, Color.white);
        set.currentColor = color;

        if (idx >= 0)
        {
            ApplyColor(cat, color, set.options[idx]);
        }
    }

    int IndexOfId(ItemCategorySet set, string id)
    {
        if (string.IsNullOrEmpty(id) || set.options == null) return -1;
        for (int i = 0; i < set.options.Length; i++)
            if (set.options[i] != null && set.options[i].id == id) return i;
        return -1;
    }
}
#endregion

// 커스터마이징 데이터를 Firebase / Photon에서 공용으로 쓰기 위한 직렬화 모델
#region class Customization Data
[Serializable]
public class CustomizationData
{

    public static CustomizationData Local;

    public string headId;
    public string bodyId;
    public string shoesId;

    public string headColor;
    public string bodyColor;
    public string shoesColor;

    public bool headAll;
    public bool bodyAll;
    public bool shoesAll;

    public PhotonTable ToPhoton()
    {
        var t = new PhotonTable
        {
            { "hId", headId ?? "" },
            { "bId", bodyId ?? "" },
            { "sId", shoesId ?? "" },
            { "hCol", headColor ?? "" },
            { "bCol", bodyColor ?? "" },
            { "sCol", shoesColor ?? "" },
            { "hAll", headAll },
            { "bAll", bodyAll },
            { "sAll", shoesAll },
        };
        
        return t;
    }

    /// <summary>
    /// 로컬플레이어의 커스터마이징 정보(스태틱)을 포톤 커스텀프로퍼티화해서 저장.
    /// </summary>
    public static void LocalToPhotonCP()
    {
        if (Local == null) { return; }
        if (Local != null) PhotonNetwork.SetPlayerCustomProperties(Local.ToPhoton());
    }

    public static bool TryFromPhoton(PhotonTable p, out CustomizationData d)
    {
        d = null;
        if (p == null) return false;
        try
        {
            d = new CustomizationData
            {
                headId = p.TryGetValue("hId", out var hId) ? (string)hId : "",
                bodyId = p.TryGetValue("bId", out var bId) ? (string)bId : "",
                shoesId = p.TryGetValue("sId", out var sId) ? (string)sId : "",
                headColor = p.TryGetValue("hCol", out var hC) ? (string)hC : "",
                bodyColor = p.TryGetValue("bCol", out var bC) ? (string)bC : "",
                shoesColor = p.TryGetValue("sCol", out var sC) ? (string)sC : "",
                headAll = p.TryGetValue("hAll", out var ha) && (bool)ha,
                bodyAll = p.TryGetValue("bAll", out var ba) && (bool)ba,
                shoesAll = p.TryGetValue("sAll", out var sa) && (bool)sa,
            };
            return true;

        }
        catch { return false; }
    }
}
#endregion

// Color ↔ Hex 문자열 변환 유틸
#region  static class Color Util
public static class ColorUtil
{
    // #RRGGBB or #RRGGBBAA
    public static string ToHex(Color c)
    {
        Color32 c32 = c;
        return $"#{c32.r:X2}{c32.g:X2}{c32.b:X2}{c32.a:X2}";
    }

    public static Color FromHexOr(string hex, Color fallback)
    {
        if (string.IsNullOrEmpty(hex)) return fallback;
        if (ColorUtility.TryParseHtmlString(hex, out var c)) return c;
        return fallback;
    }
}
#endregion