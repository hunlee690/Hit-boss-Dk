using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace HitBoss.Social.Editor
{
    public static class SocialMenuSetup
    {
        static readonly Color Panel = new Color(.065f, .085f, .12f, .97f);
        static readonly Color ButtonColor = new Color(.14f, .19f, .25f, 1);
        static readonly Color Accent = new Color(.38f, .85f, .72f, 1);
        static TMP_FontAsset font;
        static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform)); Undo.RegisterCreatedObjectUndo(go, "Build social UI");
            var rt = go.GetComponent<RectTransform>(); rt.SetParent(parent, false); rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1); rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h); return rt;
        }
        static Image Background(RectTransform rt, Color color) { var image = rt.gameObject.AddComponent<Image>(); image.color = color; return image; }
        static TMP_Text Label(Transform parent, string name, string value, float x, float y, float w, float h, float size = 16)
        {
            var rt = Rect(name, parent, x, y, w, h); var text = rt.gameObject.AddComponent<TextMeshProUGUI>(); text.font = font; text.text = value; text.fontSize = size; text.color = Color.white; text.raycastTarget = false; text.richText = false; text.overflowMode = TextOverflowModes.Truncate; return text;
        }
        static Button Button(Transform parent, string name, string value, float x, float y, float w, float h)
        {
            var rt = Rect(name, parent, x, y, w, h); var image = Background(rt, ButtonColor); var button = rt.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var label = Label(rt, "Label", value, 4, 0, w - 8, h, 14); label.alignment = TextAlignmentOptions.Center; return button;
        }
        static TMP_InputField Input(Transform parent, string name, string placeholder, float x, float y, float w, float h)
        {
            var rt = Rect(name, parent, x, y, w, h); Background(rt, new Color(.095f,.125f,.17f));
            var area = Rect("Text Area", rt, 10, 4, w-20, h-8); area.gameObject.AddComponent<RectMask2D>();
            var text = Label(area, "Text", "", 0, 0, w-20, h-8, 14); text.alignment = TextAlignmentOptions.MidlineLeft;
            var hint = Label(area, "Placeholder", placeholder, 0, 0, w-20, h-8, 13); hint.color = new Color(.6f,.67f,.74f); hint.alignment = TextAlignmentOptions.MidlineLeft;
            var input = rt.gameObject.AddComponent<TMP_InputField>(); input.textViewport = area; input.textComponent = text; input.placeholder = hint; input.characterLimit = 40; return input;
        }

        [MenuItem("Tools/Hit Boss/Build Social Menu")]
        public static void Build()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play mode before installing UI.");
            var menu = UnityEngine.Object.FindFirstObjectByType<MainMenu>();
            if (menu == null) throw new InvalidOperationException("Open the main menu scene first.");
            if (menu.mainMenu.GetComponent<SocialPanelController>() != null) throw new InvalidOperationException("Social menu is already installed.");
            Directory.CreateDirectory("Library/CodexSocialBackup");
            EditorSceneManager.SaveScene(menu.gameObject.scene, "Library/CodexSocialBackup/main menu.before-social.unity", true);
            font = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault()?.font ?? TMP_Settings.defaultFontAsset;
            var all = menu.mainMenu.GetComponentsInChildren<RectTransform>(true);
            var social = all.First(t => t.name == "social panel");
            var profile = all.First(t => t.name == "profile panel");
            var view = Undo.AddComponent<SocialPanelController>(menu.mainMenu);
            Undo.RecordObject(social, "Position social panel");
            // Parent root is the existing 100x100 menu group centered in a 1080 reference canvas.
            social.anchorMin = social.anchorMax = new Vector2(.5f,.5f); social.pivot = new Vector2(1, .5f); social.anchoredPosition = new Vector2(520,60); social.sizeDelta = new Vector2(310,350);
            social.GetComponent<Image>().color = Panel;
            var scroll = social.GetComponentInChildren<ScrollRect>(true);
            var sr = (RectTransform)scroll.transform; sr.anchorMin = sr.anchorMax = new Vector2(0,1); sr.pivot = new Vector2(0,1); sr.anchoredPosition = new Vector2(12,-145); sr.sizeDelta = new Vector2(286,148);
            scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
            if(scroll.horizontalScrollbar != null) scroll.horizontalScrollbar.gameObject.SetActive(false);
            if(scroll.verticalScrollbar != null) scroll.verticalScrollbar.gameObject.SetActive(false);
            scroll.horizontalScrollbar = null; scroll.verticalScrollbar = null;
            var viewport = scroll.viewport; viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one; viewport.offsetMin = viewport.offsetMax = Vector2.zero;
            scroll.GetComponent<Image>().color = new Color(0,0,0,0);
            var content = scroll.content; content.anchorMin = new Vector2(0,1); content.anchorMax = new Vector2(1,1); content.pivot = new Vector2(.5f,1); content.anchoredPosition = Vector2.zero; content.sizeDelta = Vector2.zero;
            var grid = content.GetComponent<GridLayoutGroup>(); if(grid != null) Undo.DestroyObjectImmediate(grid);
            var layout = Undo.AddComponent<VerticalLayoutGroup>(content.gameObject); layout.spacing = 8; layout.childControlWidth = true; layout.childControlHeight = false; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            var fitter = content.GetComponent<ContentSizeFitter>(); fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained; fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            view.content = content;
            Label(social, "Social title", "SOCIAL", 16, 14, 170, 30, 23).color = Accent;
            view.refreshButton = Button(social, "Refresh", "Retry", 225, 16, 69, 28);
            view.searchInput = Input(social, "Search username", "Exact username or name#tag", 12, 55, 222, 34);
            view.searchButton = Button(social, "Search", "Find", 240, 55, 58, 34);
            view.friendsButton = Button(social, "Friends tab", "Friends", 12, 102, 83, 34);
            view.requestsButton = Button(social, "Requests tab", "Requests", 101, 102, 108, 34);
            view.requestsLabel = view.requestsButton.GetComponentInChildren<TMP_Text>();
            view.sentButton = Button(social, "Sent tab", "Sent", 215, 102, 83, 34);
            view.emptyText = Label(social, "Empty list", "Your friends will appear here.\nSearch a username to get started.", 24, 167, 262, 90, 15); view.emptyText.alignment = TextAlignmentOptions.Center;
            view.statusText = Label(social, "Connection status", "Connecting...", 16, 305, 278, 36, 12); view.statusText.color = new Color(.65f,.74f,.8f);

            var row = Rect("Social player row", null, 0,0,286,86);
            Background(row, ButtonColor); row.gameObject.AddComponent<LayoutElement>().preferredHeight = 86;
            var rowScript = row.gameObject.AddComponent<SocialPlayerRow>();
            rowScript.profileButton = Button(row, "Open profile", "", 0,0,286,46); rowScript.profileButton.image.color = Color.clear;
            rowScript.presenceDot = Background(Rect("Presence", row, 10, 13, 7,7), Accent);
            rowScript.username = Label(row,"Username","Player",24,5,250,22,15);
            rowScript.detail = Label(row,"Presence text","Offline",24,27,245,17,12); rowScript.detail.color = new Color(.65f,.74f,.8f);
            rowScript.primaryButton = Button(row,"Primary action","Add",10,50,126,27);
            rowScript.secondaryButton = Button(row,"Secondary action","Decline",146,50,126,27);
            rowScript.primaryLabel = rowScript.primaryButton.GetComponentInChildren<TMP_Text>(); rowScript.secondaryLabel = rowScript.secondaryButton.GetComponentInChildren<TMP_Text>();
            Directory.CreateDirectory("Assets/social system/social prefabs");
            var prefab = PrefabUtility.SaveAsPrefabAsset(row.gameObject,"Assets/social system/social prefabs/Social player row.prefab");
            view.rowPrefab = prefab.GetComponent<SocialPlayerRow>(); Undo.DestroyObjectImmediate(row.gameObject);

            Undo.RecordObject(profile,"Build profile card"); profile.GetComponent<Image>().color = Panel;
            view.ownProfileButton = Undo.AddComponent<Button>(profile.gameObject);
            view.ownName = Label(profile,"Username","Your profile",16,10,220,27,20);
            view.ownName.enableAutoSizing = true; view.ownName.fontSizeMin = 12; view.ownName.fontSizeMax = 20;
            view.ownLevel = Label(profile,"Level","LEVEL 1",16,39,210,20,13); view.ownLevel.color = Accent;
            view.ownXp = Label(profile,"XP label","0 / 100 XP",16,62,210,16,11);
            var track = Rect("XP track",profile,16,84,219,5); Background(track,ButtonColor);
            view.ownProgress = Background(Rect("XP fill",track,0,0,219,5),Accent); view.ownProgress.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"); view.ownProgress.type = Image.Type.Filled; view.ownProgress.fillMethod = Image.FillMethod.Horizontal; view.ownProgress.fillOrigin = 0;
            view.existingNameInput = all.First(t => t.name == "user name").GetComponent<TMP_InputField>();
            var nr=(RectTransform)view.existingNameInput.transform; nr.sizeDelta=new Vector2(132,30); nr.anchoredPosition += new Vector2(-24,0);
            view.saveExistingNameButton=Button(nr.parent,"Save username","Save",0,0,44,30);
            var saveRect=(RectTransform)view.saveExistingNameButton.transform; saveRect.anchorMin=saveRect.anchorMax=nr.anchorMin;saveRect.pivot=nr.pivot;saveRect.anchoredPosition=nr.anchoredPosition+new Vector2(92,0);

            var canvas = menu.GetComponent<Canvas>();
            var shade = Rect("Profile viewer",canvas.transform,0,0,1080,540); shade.anchorMin=Vector2.zero;shade.anchorMax=Vector2.one;shade.offsetMin=shade.offsetMax=Vector2.zero;Background(shade,new Color(0,0,0,.7f));view.profileWindow=shade.gameObject;
            var modal=Rect("Profile details",shade,0,0,460,430);modal.anchorMin=modal.anchorMax=new Vector2(.5f,.5f);modal.pivot=new Vector2(.5f,.5f);modal.anchoredPosition=Vector2.zero;Background(modal,Panel);
            Label(modal,"Heading","PLAYER PROFILE",24,18,345,24,14).color=Accent;
            view.closeProfileButton=Button(modal,"Close","X",407,14,32,30);
            view.profileTitle=Label(modal,"Username","Your profile",24,56,412,38,27);
            view.profileStats=Label(modal,"Statistics","",24,110,412,182,19);
            view.renameInput=Input(modal,"Rename username","New username",24,306,274,35);
            view.renameButton=Button(modal,"Save name","Save name",310,306,126,35);
            view.profileFriendButton=Button(modal,"Friend action","Send friend request",24,306,412,35);view.profileFriendLabel=view.profileFriendButton.GetComponentInChildren<TMP_Text>();
            view.profileNote=Label(modal,"Profile info","",24,359,412,54,12);view.profileNote.color=new Color(.65f,.74f,.8f);
            shade.gameObject.SetActive(false);
            var system = new GameObject("Online Manager");Undo.RegisterCreatedObjectUndo(system,"Add online manager");system.AddComponent<OnlineManager>();
            EditorUtility.SetDirty(view); EditorSceneManager.MarkSceneDirty(menu.gameObject.scene); EditorSceneManager.SaveScene(menu.gameObject.scene);AssetDatabase.SaveAssets();
            Debug.Log("Social menu built and saved using existing social/profile panels.");
        }
    }
}
