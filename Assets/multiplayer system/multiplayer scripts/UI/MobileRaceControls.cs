using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
namespace HitBoss.Multiplayer.Skate
{
    public sealed class MobileRaceControls : MonoBehaviour
    {
        public GameObject controlsRoot, actionsRoot;
        public UnityEngine.UI.Image itemImage;
        public TMPro.TMP_Text itemCount;
        public Sprite bomb, mine, empty;
        public bool previewInEditor=true;
        public static MobileRaceControls Instance {get;private set;}
        public static bool Left,Right,Brake,Boost;
        static bool jump;
        static Vector2 look;
        public static float Steering => (Right?1:0)-(Left?1:0);
        public static bool TakeJump(){bool value=jump;jump=false;return value;}
        public static Vector2 TakeLook(){var value=look;look=Vector2.zero;return value;}
        SkateCombat local;
        Rect safeArea;
        void Awake(){Instance=this;ResetInput();}
        void Update()
        {
            local=SkateCombat.Players.FirstOrDefault(p=>p!=null&&p.IsSpawned&&p.IsOwner&&!p.Player.IsBot.Value);
            bool visible=(Application.isMobilePlatform||Touchscreen.current!=null||Application.isEditor&&previewInEditor)&&local!=null&&!local.Finished.Value;
            controlsRoot.SetActive(visible);
            if(safeArea!=Screen.safeArea&&Screen.width>0&&Screen.height>0)
            {
                safeArea=Screen.safeArea;var rect=(RectTransform)controlsRoot.transform;
                bool parentAlreadySafe=controlsRoot.GetComponentInParent<HitBoss.UI.PhoneSafeArea>()!=null;
                rect.anchorMin=parentAlreadySafe?Vector2.zero:new Vector2(safeArea.xMin/Screen.width,safeArea.yMin/Screen.height);
                rect.anchorMax=parentAlreadySafe?Vector2.one:new Vector2(safeArea.xMax/Screen.width,safeArea.yMax/Screen.height);
                rect.offsetMin=rect.offsetMax=Vector2.zero;
            }
            if(visible){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
            bool enabled=visible&&local.CanAct&&!local.Player.IsPaused;
            actionsRoot.SetActive(enabled);
            if(!enabled)ResetInput();
            if(itemImage!=null&&local!=null)itemImage.sprite=local.ItemCount.Value==0?empty:local.Item.Value==1?bomb:mine;
            if(itemCount!=null&&local!=null)itemCount.text=local.ItemCount.Value>1?local.ItemCount.Value.ToString():"";
        }
        public void Press(MobileRaceButton.Action action,bool held)
        {
            switch(action){case MobileRaceButton.Action.Left:Left=held;break;case MobileRaceButton.Action.Right:Right=held;break;
            case MobileRaceButton.Action.Brake:Brake=held;break;case MobileRaceButton.Action.Boost:Boost=held;break;
            case MobileRaceButton.Action.Jump:if(held)jump=true;break;
            case MobileRaceButton.Action.Slide:if(held)local?.MobileAttack(0);break;
            case MobileRaceButton.Action.Punch:if(held)local?.MobileAttack(1);break;
            case MobileRaceButton.Action.Item:local?.MobileItem(held);break;
            case MobileRaceButton.Action.Pause:if(held){ResetInput();var menu=FindFirstObjectByType<PauseMenu>();if(menu!=null){if(menu.IsPaused)menu.ResumeMatch();else menu.PauseMatch();}}break;}
        }
        public void Look(Vector2 delta){look+=delta*(1920f/Mathf.Max(1,Screen.width));}
        public static void ResetInput(){Left=Right=Brake=Boost=jump=false;look=Vector2.zero;}
        void OnApplicationFocus(bool focus){if(!focus)ResetInput();}
        void OnDisable(){ResetInput();if(Instance==this)Instance=null;}
    }
}
