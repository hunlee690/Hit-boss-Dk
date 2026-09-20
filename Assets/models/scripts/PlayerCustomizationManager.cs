using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerCustomizationManager : MonoBehaviour
{
    public enum Category
    {
        Head,
        Body,
        Bag,
        Skates
    }

    [System.Serializable]
    public class CustomizationItem
    {
        public string itemName;

        [Tooltip("Optional. Button still works without an icon.")]
        public Sprite icon;

        [Tooltip("Tick ONE item per category if it should start equipped. A category with a default item can never become empty.")]
        public bool equippedByDefault;

        [Tooltip("Only put objects belonging to THIS category/item here. Avoid shared parent objects.")]
        public GameObject[] objectsToEnable;
    }

    [System.Serializable]
    public class CategoryData
    {
        [Tooltip("If a default item is ticked, None is automatically disabled for that category.")]
        public bool allowNone = true;
        public CustomizationItem[] items;
    }

    [Header("Player")]
    public PlayerController playerController;

    [Header("Category Buttons")]
    public Button headButton;
    public Button bodyButton;
    public Button bagButton;
    public Button skatesButton;

    [Header("Scroll View")]
    public Transform itemContent;
    public Button itemButtonPrefab;

    [Header("Selected Item")]
    public TMP_Text selectedItemNameText;
    public Button equipButton;
    public TMP_Text equipButtonText;

    [Header("Head Accessories")]
    public CategoryData headAccessories;

    [Header("Body")]
    public CategoryData body;

    [Header("Bags")]
    public CategoryData bags;

    [Header("Skates")]
    public CategoryData skates;

    Category currentCategory;
    bool categoryInitialized;
    bool initialized;

    int selectedItem = -1;

    int equippedHead = -1;
    int equippedBody = -1;
    int equippedBag = -1;
    int equippedSkates = -1;


    // =====================================================
    // SETUP
    // =====================================================

    void Awake()
    {
        if (headButton != null)
            headButton.onClick.AddListener(ShowHeadAccessories);

        if (bodyButton != null)
            bodyButton.onClick.AddListener(ShowBody);

        if (bagButton != null)
            bagButton.onClick.AddListener(ShowBags);

        if (skatesButton != null)
            skatesButton.onClick.AddListener(ShowSkates);

        if (equipButton != null)
            equipButton.onClick.AddListener(EquipSelectedItem);

        if (equipButtonText == null && equipButton != null)
            equipButtonText = equipButton.GetComponentInChildren<TMP_Text>(true);
    }

    void Start()
    {
        InitializeCustomization();
    }

    public void InitializeCustomization()
    {
        if (initialized)
            return;

        initialized = true;

        equippedHead = GetStartingItem(headAccessories);
        equippedBody = GetStartingItem(body);
        equippedBag = GetStartingItem(bags);
        equippedSkates = GetStartingItem(skates);

        ApplyCategory(headAccessories, equippedHead);
        ApplyCategory(body, equippedBody);
        ApplyCategory(bags, equippedBag);

        // Equipped skates stay hidden until skate mode or customization preview.
        ApplyCategory(skates, -1);

        ApplyEquippedItemsToPlayer();
    }

    int GetStartingItem(CategoryData category)
    {
        int defaultIndex = FindDefaultItem(category);

        if (defaultIndex >= 0)
            return defaultIndex;

        // If None is not allowed, automatically use the first item.
        if (category != null &&
            !category.allowNone &&
            category.items != null &&
            category.items.Length > 0)
        {
            return 0;
        }

        return -1;
    }

    int FindDefaultItem(CategoryData category)
    {
        if (category == null || category.items == null)
            return -1;

        for (int i = 0; i < category.items.Length; i++)
        {
            if (category.items[i].equippedByDefault)
                return i;
        }

        return -1;
    }

    bool CategoryMustHaveItem(CategoryData category)
    {
        if (category == null)
            return false;

        // A default item means this category can never be empty.
        return FindDefaultItem(category) >= 0 || !category.allowNone;
    }


    // =====================================================
    // PLAYER
    // =====================================================

    public void ApplyEquippedItemsToPlayer()
    {
        if (playerController == null)
            return;

        playerController.SetEquippedSkate(
            GetItemObjects(skates, equippedSkates)
        );
    }


    // =====================================================
    // CATEGORIES
    // =====================================================

    public void ShowHeadAccessories()
    {
        OpenCategory(Category.Head, headAccessories);
    }

    public void ShowBody()
    {
        OpenCategory(Category.Body, body);
    }

    public void ShowBags()
    {
        OpenCategory(Category.Bag, bags);
    }

    public void ShowSkates()
    {
        OpenCategory(Category.Skates, skates);
    }

    void OpenCategory(Category category, CategoryData data)
    {
        // Remove preview from the previous category and put its real equipped item back.
        // This does NOT touch the other categories.
        if (categoryInitialized)
            RestoreCategory(currentCategory);

        currentCategory = category;
        categoryInitialized = true;

        selectedItem = GetEquippedIndex();

        BuildButtons(data);

        if (selectedItem >= 0)
            PreviewSelectedItem();
        else
            RestoreCategory(currentCategory);

        RefreshUI();
    }


    // =====================================================
    // BUTTONS
    // =====================================================

    void BuildButtons(CategoryData category)
    {
        ClearButtons();

        if (category == null ||
            category.items == null ||
            itemButtonPrefab == null ||
            itemContent == null)
            return;

        // Do not show NONE if this category has a default item
        // or allowNone is disabled.
        if (category.allowNone && !CategoryMustHaveItem(category))
        {
            Button button = Instantiate(itemButtonPrefab, itemContent);
            button.gameObject.SetActive(true);
            button.name = "None";

            SetButtonVisual(button, "None", null);
            button.onClick.AddListener(SelectNone);
        }

        for (int i = 0; i < category.items.Length; i++)
        {
            int index = i;
            CustomizationItem item = category.items[index];

            Button button = Instantiate(itemButtonPrefab, itemContent);
            button.gameObject.SetActive(true);
            button.name = item.itemName;

            SetButtonVisual(button, item.itemName, item.icon);
            button.onClick.AddListener(() => SelectItem(index));
        }
    }

    void SetButtonVisual(Button button, string itemName, Sprite icon)
    {
        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);

        if (text != null)
        {
            text.text = itemName;
            text.gameObject.SetActive(true);
        }

        Image[] images = button.GetComponentsInChildren<Image>(true);
        Image iconImage = null;

        foreach (Image image in images)
        {
            if (image.gameObject != button.gameObject)
            {
                iconImage = image;
                break;
            }
        }

        if (iconImage != null)
        {
            iconImage.gameObject.SetActive(icon != null);

            if (icon != null)
            {
                iconImage.sprite = icon;
                iconImage.preserveAspect = true;
            }
        }
    }

    void ClearButtons()
    {
        if (itemContent == null)
            return;

        for (int i = itemContent.childCount - 1; i >= 0; i--)
            Destroy(itemContent.GetChild(i).gameObject);
    }


    // =====================================================
    // SELECTION / PREVIEW
    // =====================================================

    void SelectItem(int index)
    {
        CategoryData data = GetCurrentCategory();

        if (data == null ||
            data.items == null ||
            index < 0 ||
            index >= data.items.Length)
            return;

        selectedItem = index;

        // Only objects from CURRENT category are changed here.
        PreviewSelectedItem();
        RefreshUI();
    }

    void PreviewSelectedItem()
    {
        CategoryData data = GetCurrentCategory();

        if (data == null ||
            data.items == null ||
            selectedItem < 0 ||
            selectedItem >= data.items.Length)
            return;

        CustomizationItem item = data.items[selectedItem];

        if (currentCategory == Category.Skates)
        {
            if (playerController != null)
            {
                playerController.SetCustomizationSkatePreview(
                    item.objectsToEnable,
                    true
                );
            }
            else
            {
                ApplyCategory(skates, selectedItem);
            }

            return;
        }

        // Head selection only swaps Head objects.
        // Body selection only swaps Body objects.
        // Bag selection only swaps Bag objects.
        ApplyCategory(data, selectedItem);
    }

    void SelectNone()
    {
        CategoryData data = GetCurrentCategory();

        // Category with a default item can NEVER become empty.
        if (CategoryMustHaveItem(data))
            return;

        selectedItem = -1;
        SetEquippedIndex(-1);

        if (currentCategory == Category.Skates)
        {
            if (playerController != null)
            {
                playerController.SetEquippedSkate(null);
                playerController.SetCustomizationSkatePreview(null, false);
            }
            else
            {
                ApplyCategory(skates, -1);
            }
        }
        else
        {
            ApplyCategory(data, -1);
        }

        RefreshUI();
    }


    // =====================================================
    // EQUIP
    // =====================================================

    void EquipSelectedItem()
    {
        CategoryData data = GetCurrentCategory();

        if (data == null ||
            data.items == null ||
            selectedItem < 0 ||
            selectedItem >= data.items.Length)
            return;

        // Clicking EQUIP on the already equipped item does nothing.
        // There is no UNEQUIP toggle anymore.
        if (GetEquippedIndex() == selectedItem)
        {
            PreviewSelectedItem();
            RefreshUI();
            return;
        }

        SetEquippedIndex(selectedItem);

        if (currentCategory == Category.Skates && playerController != null)
        {
            playerController.SetEquippedSkate(
                data.items[selectedItem].objectsToEnable
            );
        }

        PreviewSelectedItem();
        RefreshUI();
    }


    // =====================================================
    // RESTORE EQUIPPED ITEMS
    // =====================================================

    void RestoreCategory(Category category)
    {
        CategoryData data = GetCategoryData(category);
        int equipped = GetEquippedIndex(category);

        if (category == Category.Skates)
        {
            if (playerController != null)
            {
                playerController.SetCustomizationSkatePreview(null, false);
                playerController.SetEquippedSkate(
                    GetItemObjects(skates, equippedSkates)
                );
            }
            else
            {
                ApplyCategory(skates, -1);
            }

            return;
        }

        ApplyCategory(data, equipped);
    }

    void RestoreAllEquipped()
    {
        ApplyCategory(headAccessories, equippedHead);
        ApplyCategory(body, equippedBody);
        ApplyCategory(bags, equippedBag);

        if (playerController != null)
        {
            playerController.SetCustomizationSkatePreview(null, false);
            playerController.SetEquippedSkate(
                GetItemObjects(skates, equippedSkates)
            );
        }
    }

    void OnDisable()
    {
        RestoreAllEquipped();
        categoryInitialized = false;
    }


    // =====================================================
    // CATEGORY VISIBILITY
    // =====================================================

    void ApplyCategory(CategoryData category, int index)
    {
        if (category == null || category.items == null)
            return;

        // IMPORTANT:
        // This loops ONLY through the supplied category.
        // It does not disable Head when changing Body, etc.
        for (int i = 0; i < category.items.Length; i++)
        {
            bool active = i == index;
            SetItemActive(category.items[i], active, category);
        }
    }

    void SetItemActive(
        CustomizationItem item,
        bool active,
        CategoryData changingCategory)
    {
        if (item == null || item.objectsToEnable == null)
            return;

        foreach (GameObject obj in item.objectsToEnable)
        {
            if (obj == null)
                continue;

            // If the exact same object is also being used by an equipped item
            // in another category, never turn it off from this category.
            if (!active && IsUsedByAnotherEquippedCategory(obj, changingCategory))
                continue;

            obj.SetActive(active);
        }
    }

    bool IsUsedByAnotherEquippedCategory(
        GameObject obj,
        CategoryData changingCategory)
    {
        if (headAccessories != changingCategory &&
            ItemContainsObject(headAccessories, equippedHead, obj))
            return true;

        if (body != changingCategory &&
            ItemContainsObject(body, equippedBody, obj))
            return true;

        if (bags != changingCategory &&
            ItemContainsObject(bags, equippedBag, obj))
            return true;

        // Skates are handled by PlayerController because their visibility
        // also depends on skate mode / customization preview.

        return false;
    }

    bool ItemContainsObject(
        CategoryData category,
        int index,
        GameObject obj)
    {
        GameObject[] objects = GetItemObjects(category, index);

        if (objects == null)
            return false;

        foreach (GameObject itemObject in objects)
        {
            if (itemObject == obj)
                return true;
        }

        return false;
    }

    GameObject[] GetItemObjects(CategoryData category, int index)
    {
        if (category == null ||
            category.items == null ||
            index < 0 ||
            index >= category.items.Length)
            return null;

        return category.items[index].objectsToEnable;
    }


    // =====================================================
    // UI
    // =====================================================

    void RefreshUI()
    {
        CategoryData data = GetCurrentCategory();

        if (selectedItem < 0)
        {
            if (selectedItemNameText != null)
                selectedItemNameText.text = "None";

            if (equipButtonText != null)
                equipButtonText.text = "EQUIP";

            if (equipButton != null)
                equipButton.interactable = false;

            return;
        }

        if (data == null ||
            data.items == null ||
            selectedItem >= data.items.Length)
            return;

        if (selectedItemNameText != null)
            selectedItemNameText.text = data.items[selectedItem].itemName;

        bool alreadyEquipped = GetEquippedIndex() == selectedItem;

        if (equipButtonText != null)
        {
            equipButtonText.text = alreadyEquipped
                ? "EQUIPPED"
                : "EQUIP";
        }

        // Already-equipped item cannot be deselected using the Equip button.
        if (equipButton != null)
            equipButton.interactable = !alreadyEquipped;
    }


    // =====================================================
    // DATA HELPERS
    // =====================================================

    CategoryData GetCurrentCategory()
    {
        return GetCategoryData(currentCategory);
    }

    CategoryData GetCategoryData(Category category)
    {
        switch (category)
        {
            case Category.Head:
                return headAccessories;

            case Category.Body:
                return body;

            case Category.Bag:
                return bags;

            case Category.Skates:
                return skates;
        }

        return null;
    }

    int GetEquippedIndex()
    {
        return GetEquippedIndex(currentCategory);
    }

    int GetEquippedIndex(Category category)
    {
        switch (category)
        {
            case Category.Head:
                return equippedHead;

            case Category.Body:
                return equippedBody;

            case Category.Bag:
                return equippedBag;

            case Category.Skates:
                return equippedSkates;
        }

        return -1;
    }

    void SetEquippedIndex(int index)
    {
        switch (currentCategory)
        {
            case Category.Head:
                equippedHead = index;
                break;

            case Category.Body:
                equippedBody = index;
                break;

            case Category.Bag:
                equippedBag = index;
                break;

            case Category.Skates:
                equippedSkates = index;
                break;
        }
    }
}
