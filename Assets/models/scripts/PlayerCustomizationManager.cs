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

        [Tooltip("Tick ONE item per category if it should start equipped.")]
        public bool equippedByDefault;

        [Tooltip("Objects belonging to this item.")]
        public GameObject[] objectsToEnable;
    }

    [System.Serializable]
    public class CategoryData
    {
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


    private Category currentCategory;

    private bool categoryInitialized;

    private int selectedItem = -1;

    private int equippedHead = -1;
    private int equippedBody = -1;
    private int equippedBag = -1;
    private int equippedSkates = -1;


    // =====================================================
    // START
    // =====================================================
private bool initialized;

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
        equipButton.onClick.AddListener(ToggleEquip);

    if (equipButtonText == null && equipButton != null)
        equipButtonText =
            equipButton.GetComponentInChildren<TMP_Text>(true);
}


void Start()
{
    // Backup initialization in case MainMenu didn't call it.
    InitializeCustomization();
}


public void InitializeCustomization()
{
    if (initialized)
        return;

    initialized = true;

    equippedHead = FindDefaultItem(headAccessories);
    equippedBody = FindDefaultItem(body);
    equippedBag = FindDefaultItem(bags);
    equippedSkates = FindDefaultItem(skates);

    ApplyCategory(headAccessories, equippedHead);
    ApplyCategory(body, equippedBody);
    ApplyCategory(bags, equippedBag);

    // Skates stay hidden until skate mode / preview.
    ApplyCategory(skates, -1);

    ApplyEquippedItemsToPlayer();
}

public void ApplyEquippedItemsToPlayer()
{
    if (playerController == null)
    {
        Debug.LogWarning(
            "Customization Manager: PlayerController is not assigned!"
        );

        return;
    }

    GameObject[] skateObjects =
        GetItemObjects(
            skates,
            equippedSkates
        );

    playerController.SetEquippedSkate(
        skateObjects
    );

    Debug.Log(
        "Equipped skate index: " +
        equippedSkates
    );
}

    int FindDefaultItem(CategoryData category)
    {
        if (category == null ||
            category.items == null)
            return -1;


        for (int i = 0; i < category.items.Length; i++)
        {
            if (category.items[i].equippedByDefault)
                return i;
        }


        return -1;
    }


    // =====================================================
    // CATEGORIES
    // =====================================================

    public void ShowHeadAccessories()
    {
        OpenCategory(
            Category.Head,
            headAccessories
        );
    }


    public void ShowBody()
    {
        OpenCategory(
            Category.Body,
            body
        );
    }


    public void ShowBags()
    {
        OpenCategory(
            Category.Bag,
            bags
        );
    }


    public void ShowSkates()
    {
        OpenCategory(
            Category.Skates,
            skates
        );
    }


    void OpenCategory(
        Category category,
        CategoryData data)
    {
        // Restore actual equipped item from old category.
        if (categoryInitialized)
            RestoreCategory(currentCategory);


        currentCategory = category;
        categoryInitialized = true;


        selectedItem =
            GetEquippedIndex();


        BuildButtons(data);


        // Show actual equipped item first.
        if (selectedItem >= 0)
            PreviewSelectedItem();
        else
            RestoreCategory(currentCategory);


        RefreshUI();
    }


    // =====================================================
    // BUTTON CREATION
    // =====================================================

    void BuildButtons(CategoryData category)
    {
        ClearButtons();


        if (category == null ||
            category.items == null ||
            itemButtonPrefab == null ||
            itemContent == null)
            return;


        // NONE
        if (category.allowNone)
        {
            Button button =
                Instantiate(
                    itemButtonPrefab,
                    itemContent
                );

            button.gameObject.SetActive(true);
            button.name = "None";

            SetButtonVisual(
                button,
                "None",
                null
            );

            button.onClick.AddListener(
                SelectNone
            );
        }


        // ITEMS
        for (int i = 0;
             i < category.items.Length;
             i++)
        {
            int index = i;

            CustomizationItem item =
                category.items[index];


            Button button =
                Instantiate(
                    itemButtonPrefab,
                    itemContent
                );

            button.gameObject.SetActive(true);

            button.name =
                item.itemName;


            SetButtonVisual(
                button,
                item.itemName,
                item.icon
            );


            button.onClick.AddListener(
                () => SelectItem(index)
            );
        }
    }


    void SetButtonVisual(
        Button button,
        string itemName,
        Sprite icon)
    {
        TMP_Text text =
            button.GetComponentInChildren<TMP_Text>(true);

        if (text != null)
        {
            text.text = itemName;
            text.gameObject.SetActive(true);
        }


        // Try to find separate child icon.
        Image[] images =
            button.GetComponentsInChildren<Image>(true);

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
            iconImage.gameObject.SetActive(
                icon != null
            );

            if (icon != null)
            {
                iconImage.sprite = icon;
                iconImage.preserveAspect = true;
            }
        }

        // No icon = normal button + text remains visible.
    }


    void ClearButtons()
    {
        if (itemContent == null)
            return;


        for (int i =
             itemContent.childCount - 1;
             i >= 0;
             i--)
        {
            Destroy(
                itemContent.GetChild(i).gameObject
            );
        }
    }


    // =====================================================
    // SELECTION / PREVIEW
    // =====================================================

    void SelectItem(int index)
    {
        selectedItem = index;

        PreviewSelectedItem();

        RefreshUI();
    }


    void PreviewSelectedItem()
    {
        CategoryData data =
            GetCurrentCategory();


        if (data == null ||
            data.items == null ||
            selectedItem < 0 ||
            selectedItem >= data.items.Length)
            return;


        CustomizationItem item =
            data.items[selectedItem];


        // Skate preview uses PlayerController
        // so skate height can also be previewed.
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
                ApplyCategory(
                    skates,
                    selectedItem
                );
            }

            return;
        }


        // Head / Body / Bag preview immediately.
        ApplyCategory(
            data,
            selectedItem
        );
    }


    void SelectNone()
    {
        selectedItem = -1;

        SetEquippedIndex(-1);


        if (currentCategory == Category.Skates)
        {
            if (playerController != null)
            {
                playerController.SetEquippedSkate(null);

                playerController.SetCustomizationSkatePreview(
                    null,
                    false
                );
            }
            else
            {
                ApplyCategory(skates, -1);
            }
        }
        else
        {
            ApplyCategory(
                GetCurrentCategory(),
                -1
            );
        }


        RefreshUI();
    }


    // =====================================================
    // EQUIP
    // =====================================================

    void ToggleEquip()
    {
        if (selectedItem < 0)
            return;


        int equipped =
            GetEquippedIndex();


        // UNEQUIP
        if (equipped == selectedItem)
        {
            SetEquippedIndex(-1);


            // Keep item visible as PREVIEW.
            if (currentCategory == Category.Skates)
            {
                if (playerController != null)
                {
                    playerController.SetEquippedSkate(null);

                    PreviewSelectedItem();
                }
            }
        }

        // EQUIP
        else
        {
            SetEquippedIndex(
                selectedItem
            );


            if (currentCategory == Category.Skates)
            {
                CategoryData data =
                    GetCurrentCategory();


                playerController?.SetEquippedSkate(
                    data.items[selectedItem]
                        .objectsToEnable
                );
            }
        }


        // Always keep selected item visible.
        PreviewSelectedItem();

        RefreshUI();
    }


    // =====================================================
    // RESTORE REAL EQUIPPED ITEM
    // =====================================================

    void RestoreCategory(Category category)
    {
        CategoryData data =
            GetCategoryData(category);

        int equipped =
            GetEquippedIndex(category);


        if (category == Category.Skates)
        {
            if (playerController != null)
            {
                playerController.SetCustomizationSkatePreview(
                    null,
                    false
                );

                playerController.SetEquippedSkate(
                    GetItemObjects(
                        skates,
                        equippedSkates
                    )
                );
            }

            return;
        }


        ApplyCategory(
            data,
            equipped
        );
    }


    void RestoreAllEquipped()
    {
        ApplyCategory(
            headAccessories,
            equippedHead
        );

        ApplyCategory(
            body,
            equippedBody
        );

        ApplyCategory(
            bags,
            equippedBag
        );


        if (playerController != null)
        {
            playerController.SetCustomizationSkatePreview(
                null,
                false
            );

            playerController.SetEquippedSkate(
                GetItemObjects(
                    skates,
                    equippedSkates
                )
            );
        }
    }


    void OnDisable()
    {
        RestoreAllEquipped();

        categoryInitialized = false;
    }


    // =====================================================
    // ACTIVATE OBJECTS
    // =====================================================

    void ApplyCategory(
        CategoryData category,
        int index)
    {
        if (category == null ||
            category.items == null)
            return;


        for (int i = 0;
             i < category.items.Length;
             i++)
        {
            SetItemActive(
                category.items[i],
                false
            );
        }


        if (index < 0 ||
            index >= category.items.Length)
            return;


        SetItemActive(
            category.items[index],
            true
        );
    }


    void SetItemActive(
        CustomizationItem item,
        bool active)
    {
        if (item.objectsToEnable == null)
            return;


        foreach (GameObject obj
                 in item.objectsToEnable)
        {
            if (obj != null)
                obj.SetActive(active);
        }
    }


    GameObject[] GetItemObjects(
        CategoryData category,
        int index)
    {
        if (category == null ||
            category.items == null ||
            index < 0 ||
            index >= category.items.Length)
            return null;


        return category.items[index]
            .objectsToEnable;
    }


    // =====================================================
    // UI
    // =====================================================

    void RefreshUI()
    {
        CategoryData data =
            GetCurrentCategory();


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
            selectedItem >= data.items.Length)
            return;


        if (selectedItemNameText != null)
        {
            selectedItemNameText.text =
                data.items[selectedItem].itemName;
        }


        if (equipButtonText != null)
        {
            equipButtonText.text =
                GetEquippedIndex() == selectedItem
                    ? "UNEQUIP"
                    : "EQUIP";
        }


        if (equipButton != null)
            equipButton.interactable = true;
    }


    // =====================================================
    // DATA HELPERS
    // =====================================================

    CategoryData GetCurrentCategory()
    {
        return GetCategoryData(
            currentCategory
        );
    }


    CategoryData GetCategoryData(
        Category category)
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
        return GetEquippedIndex(
            currentCategory
        );
    }


    int GetEquippedIndex(
        Category category)
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