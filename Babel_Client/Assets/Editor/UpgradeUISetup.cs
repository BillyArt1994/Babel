using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class UpgradeUISetup
{
    [MenuItem("Babel/Setup Upgrade Card Prefab")]
    public static void SetupUpgradeCardPrefab()
    {
        // 1. 找到场景中的 UpgradeUI GameObject
        GameObject upgradeUI = GameObject.Find("UpgradeUI");
        if (upgradeUI == null)
        {
            Debug.LogError("[UpgradeUISetup] 场景中找不到 UpgradeUI GameObject。");
            return;
        }

        // 2. 在 UpgradeUI 下创建或找到 CardContainer 子物体
        Transform cardContainerTf = upgradeUI.transform.Find("CardContainer");
        if (cardContainerTf == null)
        {
            GameObject cardContainerGo = new GameObject("CardContainer", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(cardContainerGo, "Create CardContainer");
            cardContainerGo.transform.SetParent(upgradeUI.transform, false);
            cardContainerTf = cardContainerGo.transform;
        }

        RectTransform cardContainerRect = cardContainerTf as RectTransform;
        if (cardContainerRect == null)
            cardContainerRect = cardContainerTf.gameObject.GetComponent<RectTransform>();

        cardContainerRect.anchorMin = new Vector2(0.05f, 0.15f);
        cardContainerRect.anchorMax = new Vector2(0.95f, 0.85f);
        cardContainerRect.anchoredPosition = Vector2.zero;
        cardContainerRect.sizeDelta = Vector2.zero;

        HorizontalLayoutGroup hlg = cardContainerTf.GetComponent<HorizontalLayoutGroup>();
        if (hlg == null)
            hlg = cardContainerTf.gameObject.AddComponent<HorizontalLayoutGroup>();

        hlg.spacing = 30f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;
        hlg.padding = new RectOffset(40, 40, 20, 20);

        // 3. 删除 UpgradeUI 下的 Card0, Card1, Card2
        string[] cardNames = { "Card0", "Card1", "Card2" };
        foreach (string cardName in cardNames)
        {
            Transform card = upgradeUI.transform.Find(cardName);
            if (card != null)
            {
                Undo.DestroyObjectImmediate(card.gameObject);
                Debug.Log($"[UpgradeUISetup] 已删除子物体: {cardName}");
            }
        }

        // 4. 找到 UpgradeSelectionUI 组件并设置字段
        Component upgradeSelectionUI = upgradeUI.GetComponent("UpgradeSelectionUI");
        if (upgradeSelectionUI == null)
        {
            Debug.LogError("[UpgradeUISetup] 在 UpgradeUI 上找不到 UpgradeSelectionUI 组件。");
            return;
        }

        // 加载 Card0.prefab 并获取 UpgradeCardUI 组件
        GameObject cardPrefabGo = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/UpgradeCard.prefab");
        if (cardPrefabGo == null)
        {
            Debug.LogError("[UpgradeUISetup] 找不到 prefab: Assets/Prefabs/UI/Card0.prefab");
            return;
        }

        Component cardPrefabComp = cardPrefabGo.GetComponent("UpgradeCardUI");
        if (cardPrefabComp == null)
        {
            Debug.LogError("[UpgradeUISetup] Card0.prefab 上找不到 UpgradeCardUI 组件。");
            return;
        }

        // 使用 SerializedObject 设置私有 [SerializeField] 字段
        SerializedObject so = new SerializedObject(upgradeSelectionUI);

        SerializedProperty propCardContainer = so.FindProperty("_cardContainer");
        if (propCardContainer != null)
            propCardContainer.objectReferenceValue = cardContainerTf;
        else
            Debug.LogWarning("[UpgradeUISetup] 未找到字段 _cardContainer。");

        SerializedProperty propCardPrefab = so.FindProperty("_cardPrefab");
        if (propCardPrefab != null)
            propCardPrefab.objectReferenceValue = cardPrefabComp;
        else
            Debug.LogWarning("[UpgradeUISetup] 未找到字段 _cardPrefab。");

        // _root 保持原值，不修改
        so.ApplyModifiedProperties();

        // 5. 标记场景为 dirty
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        // 6. 打印完成提示
        Debug.Log("[UpgradeUISetup] Setup 完成：CardContainer 已配置，UpgradeSelectionUI 字段已设置。");
    }

    [MenuItem("Babel/Setup Active Skill Icon")]
    public static void SetupActiveSkillIcon()
    {
        // 1. 找到 Canvas
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[UpgradeUISetup] 场景中找不到 Canvas。");
            return;
        }

        // 2. 找到 SkillList 作为定位参考
        Transform skillList = canvas.transform.Find("SkillList");

        // 3. 创建 ActiveSkillIcon（如果不存在）
        Transform existing = canvas.transform.Find("ActiveSkillIcon");
        if (existing != null)
        {
            Debug.Log("[UpgradeUISetup] ActiveSkillIcon 已存在，跳过创建。");
        }
        else
        {
            GameObject iconGo = new GameObject("ActiveSkillIcon", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(iconGo, "Create ActiveSkillIcon");
            iconGo.transform.SetParent(canvas.transform, false);

            // 添加 Image 组件
            Image img = iconGo.AddComponent<Image>();
            img.color = Color.white;
            img.raycastTarget = false;

            // 右上角定位，在 SkillList 上方
            RectTransform rt = iconGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-15f, -15f);
            rt.sizeDelta = new Vector2(48f, 48f);

            // 如果 SkillList 存在，把 SkillList 下移给 icon 留空间
            if (skillList != null)
            {
                RectTransform slrt = skillList as RectTransform;
                if (slrt != null)
                    slrt.anchoredPosition = new Vector2(slrt.anchoredPosition.x, -15f - 48f - 8f);
            }

            existing = iconGo.transform;
        }

        // 4. 连接到 GameHUD._activeSkillIcon
        Component gameHUD = Object.FindObjectOfType<GameHUD>();
        if (gameHUD == null)
        {
            Debug.LogError("[UpgradeUISetup] 场景中找不到 GameHUD 组件。");
            return;
        }

        SerializedObject so = new SerializedObject(gameHUD);
        SerializedProperty prop = so.FindProperty("_activeSkillIcon");
        if (prop != null)
        {
            prop.objectReferenceValue = existing.GetComponent<Image>();
            so.ApplyModifiedProperties();
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log("[UpgradeUISetup] ActiveSkillIcon 创建完成并连接到 GameHUD。");
    }
}
