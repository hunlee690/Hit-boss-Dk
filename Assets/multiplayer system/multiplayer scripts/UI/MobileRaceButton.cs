using UnityEngine;
using UnityEngine.EventSystems;
namespace HitBoss.Multiplayer.Skate
{
    public sealed class MobileRaceButton : MonoBehaviour,IPointerDownHandler,IPointerUpHandler,IDragHandler,IInitializePotentialDragHandler
    {
        public enum Action {Left,Right,Jump,Slide,Punch,Item,Boost,Brake,Pause,Look}
        public Action action;
        public MobileRaceControls controls;
        int pointer=int.MinValue;
        public void OnInitializePotentialDrag(PointerEventData e){e.useDragThreshold=false;}
        public void OnPointerDown(PointerEventData e){if(pointer!=int.MinValue)return;pointer=e.pointerId;controls.Press(action,true);}
        public void OnPointerUp(PointerEventData e){if(pointer!=e.pointerId)return;pointer=int.MinValue;controls.Press(action,false);}
        public void OnDrag(PointerEventData e){if(pointer==e.pointerId&&(action==Action.Look||action==Action.Item))controls.Look(e.delta);}
        void OnDisable(){if(pointer!=int.MinValue){pointer=int.MinValue;if(controls!=null)controls.Press(action,false);}}
    }
}
