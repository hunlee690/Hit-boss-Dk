using TMPro;
using UnityEngine;

// Decorative motion only. These scene objects never affect racing or collisions.
public sealed class ArenaAmbientProp : MonoBehaviour
{
    public enum Animation { Flag, Spin, Hover, Orbit, Sweep, Cheer, Billboard }
    public Animation animation;
    public float speed=1f, amplitude=.2f, phase;
    public Vector3 axis=Vector3.up;
    public TMP_Text boardText;
    public string[] messages={"HIT BOSS", "RACE • SLIDE • WIN", "THREE LAPS • ONE CHAMPION"};
    Vector3 origin;
    Quaternion rotation;
    Mesh flag;
    Vector3[] rest, deformed;
    int lastMessage=-1;

    void OnEnable()
    {
        origin=transform.localPosition;rotation=transform.localRotation;
        if(animation!=Animation.Flag)return;
        var filter=GetComponent<MeshFilter>();
        if(filter==null||filter.sharedMesh==null){enabled=false;return;}
        flag=Instantiate(filter.sharedMesh);flag.name="Waving flag instance";
        filter.sharedMesh=flag;rest=flag.vertices;deformed=new Vector3[rest.Length];
    }
    void Update()
    {
        float t=Time.time*speed+phase;
        switch(animation)
        {
            case Animation.Flag:
                for(int i=0;i<rest.Length;i++)
                {
                    var p=rest[i];
                    float freedom=Mathf.Clamp01(p.x/Mathf.Max(.01f,flag.bounds.size.x));
                    p.z+=Mathf.Sin(t*2.4f-p.x*2f+p.y*.6f)*amplitude*freedom;
                    deformed[i]=p;
                }
                flag.vertices=deformed;flag.RecalculateNormals();break;
            case Animation.Spin: transform.localRotation=rotation*Quaternion.AngleAxis(t*60f,axis.normalized);break;
            case Animation.Hover: transform.localPosition=origin+Vector3.up*Mathf.Sin(t)*amplitude;break;
            case Animation.Orbit:
                transform.localPosition=origin+new Vector3(Mathf.Sin(t*.12f)*amplitude,Mathf.Sin(t*.8f)*.3f,Mathf.Cos(t*.12f)*amplitude);
                transform.localRotation=rotation*Quaternion.Euler(0,t*.12f*Mathf.Rad2Deg,0);break;
            case Animation.Sweep: transform.localRotation=rotation*Quaternion.Euler(Mathf.Sin(t*.4f)*amplitude,Mathf.Sin(t*.26f)*amplitude,0);break;
            case Animation.Cheer: transform.localRotation=rotation*Quaternion.AngleAxis(Mathf.Sin(t*2f)*amplitude,axis.normalized);break;
            case Animation.Billboard:
                if(boardText==null||messages==null||messages.Length==0)break;
                int index=Mathf.FloorToInt(t/6f)%messages.Length;
                if(index<0)index+=messages.Length;
                if(index!=lastMessage){boardText.text=messages[index];lastMessage=index;}break;
        }
    }
    void OnDestroy(){if(flag!=null)Destroy(flag);}
}
