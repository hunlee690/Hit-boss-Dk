using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace HitBoss.Multiplayer.Skate
{
    public sealed class SkateRaceResult
    {
        public string Name;
        public ulong PlayerId;
        public int Place, Kills, Coins;
        public double Seconds;
        public float Progress;
        public bool Finished, Disconnected;
    }

    // Presentation only. Race order, rewards and attack readiness come from the host.
    public sealed class SkateRaceFeedback : MonoBehaviour
    {
        [SerializeField] MatchHUD hud;
        SkateMatchRules rules;
        [SerializeField] Transform canvas;
        [SerializeField] TMP_Text banner, summary, leftResults, rightResults, countdown;
        [SerializeField] TMP_Text[] podium = new TMP_Text[3];
        [SerializeField] TMP_Text slide, punch, item;
        [SerializeField] RectTransform slideCover, punchCover;
        [SerializeField] UnityEngine.UI.Image itemIcon;
        public Sprite bombIcon, mineIcon;
        [Header("Editable scene UI")]
        public RectTransform boostMeter, boostFill;
        public TMP_Text boostLabel, killNotice;
        public UnityEngine.UI.Button leaveMatchButton;
        public bool SceneReady => artwork != null && boostFill != null && leaveMatchButton != null;
        readonly List<RectTransform> confetti = new List<RectTransform>();
        readonly List<Vector2> confettiSpeed = new List<Vector2>();
        SkateCombat local;
        int lastLap;
        bool finishShown;
        bool controlsVisible = true;
        float bannerUntil, confettiUntil;
        AudioSource audioSource;
        AudioClip finishSound;
        [SerializeField] RaceHUDArtwork artwork;

        public void Bind(MatchHUD sceneHud, SkateMatchRules match)
        {
            hud=sceneHud;rules=match;canvas=transform;
            artwork.Bind(sceneHud,match);
            banner.gameObject.SetActive(false);
        }

        public void Initialize(MatchHUD sceneHud, SkateMatchRules match, Transform root)
        {
            hud = sceneHud; rules = match; canvas = root;
            if (hud.healthText == null) return;
            banner = Label("Race announcement", canvas, new Vector2(.2f,.73f), new Vector2(.8f,.83f), 30);
            banner.gameObject.SetActive(false);
            slide = Ability("SLIDE · LMB", 0, out slideCover);
            punch = Ability("PUNCH · RMB", 1, out punchCover);
            var itemRoot = Panel("Item indicator", canvas, new Vector2(.015f,.165f), new Vector2(.20f,.23f), new Color(.03f,.07f,.12f,.85f));
            if(rules.uiTheme!=null)Anchor(itemRoot,new Vector2(.80f,.89f),new Vector2(.99f,.985f));
            item = Label("Item count", itemRoot, new Vector2(.3f,0), Vector2.one, 16);
            var image = new GameObject("Item icon", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            image.transform.SetParent(itemRoot,false); itemIcon = image.GetComponent<UnityEngine.UI.Image>();
            Anchor(itemIcon.rectTransform, Vector2.zero, new Vector2(.3f,1)); itemIcon.raycastTarget = false;
            itemIcon.preserveAspect = true;
            bombIcon=Icon(0);mineIcon=Icon(1);
            if (rules.uiTheme != null)
            {
                artwork=gameObject.AddComponent<RaceHUDArtwork>();
                artwork.Initialize(rules.uiTheme,hud,rules,canvas);
                RaceHUDArtwork.Skin(itemRoot,rules.uiTheme.purpleButton);
                var frame=RaceHUDArtwork.Picture("Item slot",itemRoot,rules.uiTheme.iconFrame,new Vector2(.025f,.08f),new Vector2(.26f,.92f));
                frame.transform.SetAsFirstSibling();
                Anchor(itemIcon.rectTransform,new Vector2(.06f,.16f),new Vector2(.23f,.84f));
                itemIcon.transform.SetAsLastSibling();
            }

            if (hud.endPanel == null || hud.resultText == null) return;
            if (artwork != null) return;
            var parent = hud.endPanel.transform;
            var background = hud.endPanel.GetComponent<UnityEngine.UI.Image>();
            if (background != null && rules.uiTheme == null) background.color = new Color(.025f,.045f,.09f,.96f);
            summary = Label("Personal race summary",parent,new Vector2(.025f,.72f),new Vector2(.975f,.82f),22);
            for (int place=1; place<=3; place++)
            {
                int column=place==1?1:place==2?0:2;
                float x=.08f+column*.285f;
                Color color=place==1?new Color(.38f,.27f,.06f,.98f):place==2?new Color(.19f,.24f,.31f,.98f):new Color(.31f,.16f,.09f,.98f);
                var panel=Panel("Podium "+place,parent,new Vector2(x,.55f),new Vector2(x+.265f,place==1?.72f:place==2?.69f:.66f),color);
                podium[place-1]=Label("Winner",panel,Vector2.zero,Vector2.one,22);
                if(rules.uiTheme!=null)
                {
                    RaceHUDArtwork.Skin(panel,rules.uiTheme.podiumBanners[place-1]);
                    RaceHUDArtwork.Picture("Medal",panel,rules.uiTheme.medals[place-1],new Vector2(.015f,.12f),new Vector2(.22f,.88f));
                    Anchor(podium[place-1].rectTransform,new Vector2(.23f,.04f),new Vector2(.97f,.96f));
                }
            }
            leftResults=Label("Results 1 to 10",parent,new Vector2(.025f,.18f),new Vector2(.49f,.54f),17);
            rightResults=Label("Results 11 to 20",parent,new Vector2(.51f,.18f),new Vector2(.975f,.54f),17);
            leftResults.alignment=rightResults.alignment=TextAlignmentOptions.TopLeft;
            countdown=Label("Return countdown",parent,new Vector2(.15f,.145f),new Vector2(.85f,.185f),15);
        }

        TMP_Text Ability(string title,int index,out RectTransform cover)
        {
            float x=.015f+index*.155f;
            var panel=Panel(title,canvas,new Vector2(x,.075f),new Vector2(x+.145f,.145f),new Color(.03f,.07f,.12f,.85f));
            if(rules.uiTheme!=null)RaceHUDArtwork.Skin(panel,rules.uiTheme.abilityPanel);
            cover=Panel("Cooldown",panel,Vector2.zero,Vector2.one,new Color(.2f,.47f,.58f,.55f));
            var label=Label("Label",panel,Vector2.zero,Vector2.one,15,title);
            if(index==1&&rules.uiTheme!=null)
            {
                RaceHUDArtwork.Picture("Punch icon",panel,rules.uiTheme.punch,new Vector2(.025f,.15f),new Vector2(.20f,.85f));
                Anchor(label.rectTransform,new Vector2(.21f,0),Vector2.one);
            }
            return label;
        }

        TMP_Text Label(string name,Transform parent,Vector2 min,Vector2 max,float size,string text="")
        {
            TMP_Text template=hud.resultText!=null?hud.resultText:hud.healthText;
            var label=Instantiate(template,parent); label.name=name; label.gameObject.SetActive(true);
            label.text=text;label.richText=false;label.raycastTarget=false;label.color=Color.white;
            label.alignment=TextAlignmentOptions.Center;label.enableAutoSizing=true;
            label.fontSizeMin=10;label.fontSizeMax=size;label.textWrappingMode=TextWrappingModes.NoWrap;
            label.overflowMode=TextOverflowModes.Truncate;Anchor(label.rectTransform,min,max);
            label.rectTransform.offsetMin=new Vector2(6,2);label.rectTransform.offsetMax=new Vector2(-6,-2);
            return label;
        }

        static RectTransform Panel(string name,Transform parent,Vector2 min,Vector2 max,Color color)
        {
            var obj=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image));
            var image=obj.GetComponent<UnityEngine.UI.Image>();image.color=color;image.raycastTarget=false;
            image.transform.SetParent(parent,false);Anchor(image.rectTransform,min,max);return image.rectTransform;
        }
        static void Anchor(RectTransform rect,Vector2 min,Vector2 max)
        {rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;rect.localScale=Vector3.one;}

        public void Notice(string text,Color color,float seconds)
        {
            if (banner==null) return;
            banner.text=text;banner.color=color;bannerUntil=Time.unscaledTime+seconds;banner.gameObject.SetActive(true);
        }
        public void UpdateRace(SkateCombat player)
        {
            local=player;
            int lap=player.CompletedLaps.Value;
            if (lap>lastLap && !player.HasFinishedRace && !player.Finished.Value)
                Notice(lap==rules.RequiredLaps-1?"FINAL LAP!":"LAP "+(lap+1)+" / "+rules.RequiredLaps,new Color(.65f,1f,.3f),2.2f);
            lastLap=lap;
            if (!finishShown && player.HasFinishedRace)
            {
                finishShown=true;PlayFinishSound();
                if (player.FinishPlace.Value<=3) Celebrate();
            }
        }

        public void SetControlsVisible(bool visible,bool showItem)
        {
            if (slideCover == null) return;
            controlsVisible = visible;
            slideCover.parent.gameObject.SetActive(visible);
            punchCover.parent.gameObject.SetActive(visible);
            itemIcon.transform.parent.gameObject.SetActive(showItem);
            if (!visible && banner != null) banner.gameObject.SetActive(false);
        }

        public void DrawResults(SkateCombat player,SkateRaceResult[] rows,int secondsToMenu)
        {
            if(artwork!=null){artwork.Results(player,rows,secondsToMenu);return;}
            if (hud.resultText==null || summary==null) return;
            var title=hud.resultText;
            Anchor(title.rectTransform,new Vector2(.025f,.82f),new Vector2(.975f,.98f));
            title.fontSizeMax=42;title.fontSizeMin=20;title.richText=false;
            title.text=player.HasFinishedRace?"CONGRATULATIONS!  YOU FINISHED "+Ordinal(player.FinishPlace.Value).ToUpperInvariant():"RACE COMPLETE · DID NOT FINISH";
            title.color=player.HasFinishedRace?new Color(1f,.87f,.35f):Color.white;
            summary.text=(player.HasFinishedRace?"TIME "+TimeLabel(player.FinishSeconds.Value)+"     ":"")+"KILLS "+player.Kills.Value+"     +"+player.CoinsEarned.Value+" COINS";
            for (int i=0;i<3;i++)
            {
                var winner=rows.FirstOrDefault(r=>r.Finished && r.Place==i+1);
                podium[i].text="#"+(i+1)+"\n"+(winner==null?(rules.Running?"Waiting…":"No finisher"):winner.Name+"\n"+TimeLabel(winner.Seconds));
            }
            string Row(SkateRaceResult row) => (row.Finished?row.Place.ToString("00"):"—")+"  "+row.Name+"  ·  "+(row.Finished?TimeLabel(row.Seconds):row.Disconnected?"LEFT":rules.Running?"RACING":"DNF")+"  ·  "+row.Kills+" K";
            leftResults.text=string.Join("\n",rows.Take(10).Select(Row));
            rightResults.text=string.Join("\n",rows.Skip(10).Take(10).Select(Row));
            countdown.text=secondsToMenu>=0?"MAIN MENU IN "+secondsToMenu+"s":"OTHER RACERS ARE STILL FINISHING";
        }
        public void DrawArtwork(SkateCombat player,bool results,bool paused) => artwork?.Draw(player,results,paused);
        public static string Ordinal(int place)
        {
            int tail=place%100;
            return place+(tail>=11 && tail<=13?"th":place%10==1?"st":place%10==2?"nd":place%10==3?"rd":"th");
        }
        public static string TimeLabel(double seconds)
        {
            int hundredths=Mathf.Max(0,(int)System.Math.Round(seconds*100));
            return (hundredths/6000).ToString("00")+":"+(hundredths/100%60).ToString("00")+"."+(hundredths%100).ToString("00");
        }

        void Update()
        {
            if (banner!=null && (Time.unscaledTime>=bannerUntil || !controlsVisible)) banner.gameObject.SetActive(false);
            if (local!=null && local.IsSpawned && slide!=null)
            {
                float slideWait=local.CooldownRemaining(0), punchWait=local.CooldownRemaining(1);
                slideCover.anchorMax=new Vector2(Mathf.Clamp01(local.CooldownFraction(0)),1);
                punchCover.anchorMax=new Vector2(Mathf.Clamp01(local.CooldownFraction(1)),1);
                slide.text="SLIDE · LMB"+(slideWait>.01f?"  "+slideWait.ToString("F1")+"s":"  READY");
                punch.text="PUNCH · RMB"+(punchWait>.01f?"  "+punchWait.ToString("F1")+"s":"  READY");
                int kind=local.Item.Value;
                item.text=kind==1?"BOMB ×"+local.ItemCount.Value+" · HOLD MMB":kind==2?"MINES ×"+local.ItemCount.Value+" · CLICK MMB":artwork!=null?"":"ITEM EMPTY";
                itemIcon.enabled=kind>0;itemIcon.sprite=kind==2?mineIcon:bombIcon;
            }
            for (int i=confetti.Count-1;i>=0;i--)
            {
                var piece=confetti[i];
                if (Time.unscaledTime>=confettiUntil){Destroy(piece.gameObject);confetti.RemoveAt(i);confettiSpeed.RemoveAt(i);continue;}
                confettiSpeed[i]+=Vector2.down*95f*Time.unscaledDeltaTime;
                piece.anchoredPosition+=confettiSpeed[i]*Time.unscaledDeltaTime;
                piece.Rotate(0,0,(i%2==0?1:-1)*140f*Time.unscaledDeltaTime);
            }
        }
        void Celebrate()
        {
            confettiUntil=Time.unscaledTime+2.5f;
            var rect=(RectTransform)canvas;float width=rect.rect.width;
            for (int i=0;i<32;i++)
            {
                var piece=Panel("Finish confetti",canvas,new Vector2(.5f,.8f),new Vector2(.5f,.8f),Color.HSVToRGB((i*.137f)%1,.65f,1));
                piece.sizeDelta=new Vector2(7,14);piece.anchoredPosition=new Vector2((i%2==0?-1:1)*width*.32f,0);
                confetti.Add(piece);confettiSpeed.Add(new Vector2((i%2==0?1:-1)*Random.Range(50,180),Random.Range(60,180)));
            }
        }
        void PlayFinishSound()
        {
            audioSource=gameObject.AddComponent<AudioSource>();audioSource.spatialBlend=0;audioSource.volume=.18f;audioSource.playOnAwake=false;
            const int rate=22050;float[] samples=new float[rate];float[] notes={659.25f,830.61f,987.77f,1318.51f};
            for(int i=0;i<samples.Length;i++)
            {
                float t=(float)i/rate;int note=Mathf.Min(3,(int)(t/.22f));float phase=t-note*.22f;
                samples[i]=Mathf.Sin(t*notes[note]*Mathf.PI*2)*Mathf.Clamp01(phase/.015f)*Mathf.Exp(-phase*9f)*.5f;
            }
            finishSound=AudioClip.Create("Race finish chime",samples.Length,1,rate,false);finishSound.SetData(samples,0);audioSource.PlayOneShot(finishSound);
        }
        static Sprite Icon(int kind)
        {
            var texture=new Texture2D(48,48,TextureFormat.RGBA32,false){name="Race item icon",filterMode=FilterMode.Bilinear};
            var pixels=new Color[48*48];
            for(int y=0;y<48;y++) for(int x=0;x<48;x++)
            {
                bool body=kind==0?(x-23)*(x-23)+(y-21)*(y-21)<225:kind==1?((x-24)*(x-24)/1.8f+(y-19)*(y-19)*2)<260:Mathf.Abs(x-24)+Mathf.Abs(y-24)<16;
                bool fuse=kind==0&&x>=29&&x<=33&&y>=31&&y<=41;
                bool light=kind==1&&(x-24)*(x-24)+(y-27)*(y-27)<25;
                pixels[y*48+x]=light?new Color(1,.25f,.1f):fuse?new Color(1,.75f,.2f):body?new Color(.7f,.88f,1):Color.clear;
            }
            texture.SetPixels(pixels);texture.Apply();return Sprite.Create(texture,new Rect(0,0,48,48),new Vector2(.5f,.5f));
        }
        void OnDestroy()
        {
            if(finishSound!=null)Destroy(finishSound);
        }
    }
}
