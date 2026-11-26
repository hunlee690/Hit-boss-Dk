using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace HitBoss.Multiplayer.Skate
{
    // Artwork only. All ranks, health, rewards and finish state still come from the host.
    public sealed class RaceHUDArtwork : MonoBehaviour
    {
        [SerializeField] RaceUITheme theme;
        [SerializeField] MatchHUD hud;
        SkateMatchRules rules;
        [SerializeField] RectTransform healthRoot, raceRoot, standingsRoot, finishRoot;
        [SerializeField] UnityEngine.UI.Image[] hearts = new UnityEngine.UI.Image[3];
        [SerializeField] UnityEngine.UI.Image start, boostEmpty, emptyItem, finishRank, spectating;
        [SerializeField] TMP_Text healthCount, placeText, extraLap, rankText, returnText;
        [SerializeField] TMP_Text[] statistics=new TMP_Text[3], podiumNames=new TMP_Text[3], podiumTimes=new TMP_Text[3];
        [SerializeField] Row[] standings, resultRows;
        [SerializeField] Material textMaterial;
        [SerializeField,Range(4f,30f)] float standingsMoveSpeed=12f;
        const int VisibleStandings=10;
        readonly Dictionary<ulong,Row> standingsByRacer=new Dictionary<ulong,Row>();
        readonly HashSet<Row> visibleStandingRows=new HashSet<Row>();
        Vector2[] standingSlotMin,standingSlotMax;
        public Material TextMaterial => textMaterial;
        [System.Serializable] sealed class Row
        {
            public RectTransform Root;
            public UnityEngine.UI.Image Background;
            public TMP_Text Rank, Name, Value, Tail;
        }

        public void Bind(MatchHUD sceneHud,SkateMatchRules match)
        {hud=sceneHud;rules=match;CacheStandingSlots();}

        public void Initialize(RaceUITheme artwork, MatchHUD sceneHud, SkateMatchRules match, Transform canvas)
        {
            theme=artwork;hud=sceneHud;rules=match;
            if(hud.healthText.fontSharedMaterial!=null)
            {
                textMaterial=new Material(hud.healthText.fontSharedMaterial){name="Race UI outlined text"};
                textMaterial.SetColor(ShaderUtilities.ID_OutlineColor,new Color(.025f,.025f,.08f,1));
                textMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth,.15f);
            }
            healthRoot=Plate("Health capsule",canvas,theme.abilityPanel,new Vector2(.01f,.88f),new Vector2(.18f,.985f));
            for(int i=0;i<3;i++)hearts[i]=Picture("Heart "+(i+1),healthRoot,theme.fullHeart,new Vector2(.06f+i*.18f,.18f),new Vector2(.23f+i*.18f,.82f));
            Divider(healthRoot,.68f);
            healthCount=Label("Health count",healthRoot,new Vector2(.72f,.15f),new Vector2(.97f,.85f),36);

            raceRoot=Plate("Position and lap capsule",canvas,theme.abilityPanel,new Vector2(.385f,.88f),new Vector2(.615f,.985f));
            placeText=Label("Race place",raceRoot,new Vector2(.055f,.13f),new Vector2(.53f,.87f),48);placeText.richText=true;
            Divider(raceRoot,.55f);
            Picture("Lap",raceRoot,theme.lap,new Vector2(.61f,.29f),new Vector2(.77f,.71f));
            extraLap=Label("Lap numbers",raceRoot,new Vector2(.78f,.20f),new Vector2(.98f,.80f),30);

            standingsRoot=Plate("Race standings",canvas,theme.leaderboard,new Vector2(.01f,.16f),new Vector2(.205f,.855f));
            standings=MakeRows(standingsRoot,false);
            CacheStandingSlots();
            if(hud.rankingText!=null)hud.rankingText.text="";
            start=Picture("Race countdown artwork",canvas,null,new Vector2(.41f,.43f),new Vector2(.59f,.66f));start.enabled=false;
            spectating=Picture("Spectating icon",canvas,theme.spectator,new Vector2(.475f,.79f),new Vector2(.525f,.85f));spectating.enabled=false;

            var meter=canvas.Find("BoostMeter");
            if(meter!=null)
            {
                Skin(meter,theme.abilityPanel);Skin(meter.Find("Boost reserve track/BoostFill"),theme.boostFill);
                var badge=Plate("Boost badge",meter,theme.abilityPanel,new Vector2(-.025f,-.10f),new Vector2(.14f,1.10f));
                Picture("Boost icon",badge,theme.boostIcon,new Vector2(.15f,.1f),new Vector2(.85f,.9f));
                Divider(meter,.16f);
                boostEmpty=Picture("Boost empty artwork",meter,theme.boostEmpty,new Vector2(.34f,.22f),new Vector2(.82f,.78f));
            }
            var itemRoot=canvas.Find("Item indicator");
            if(itemRoot!=null)
            {
                Divider(itemRoot,.29f);
                emptyItem=Picture("Empty item artwork",itemRoot,theme.itemEmpty,new Vector2(.35f,.25f),new Vector2(.94f,.75f));
            }
            BuildResults();
        }

        void BuildResults()
        {
            if(hud.endPanel==null)return;
            var parent=hud.endPanel.transform;
            Anchor(hud.resultText.rectTransform,new Vector2(.16f,.87f),new Vector2(.84f,1.02f));
            Skin(parent,theme.panel);
            finishRoot=Plate("Finish title banner",parent,theme.titleBanner,new Vector2(.19f,.86f),new Vector2(.81f,1.045f));
            var wingLeft=Picture("Title ribbon left",parent,theme.titleWing,new Vector2(.12f,.94f),new Vector2(.26f,1.02f));
            var wingRight=Picture("Title ribbon right",parent,theme.titleWing,new Vector2(.74f,.94f),new Vector2(.88f,1.02f));wingRight.rectTransform.localScale=new Vector3(-1,1,1);
            wingLeft.transform.SetSiblingIndex(finishRoot.GetSiblingIndex());wingRight.transform.SetSiblingIndex(finishRoot.GetSiblingIndex());
            Picture("Congratulations",finishRoot,theme.congratulations,new Vector2(.035f,.42f),new Vector2(.965f,.94f));
            Picture("You",finishRoot,theme.you,new Vector2(.20f,.06f),new Vector2(.33f,.37f));
            Picture("Finished",finishRoot,theme.finished,new Vector2(.35f,.06f),new Vector2(.65f,.37f));
            finishRank=Picture("Finishing rank",finishRoot,null,new Vector2(.68f,.06f),new Vector2(.84f,.38f));
            rankText=Label("Other finishing rank",finishRoot,new Vector2(.68f,.03f),new Vector2(.88f,.40f),48);
            Picture("Trophy",parent,theme.trophy,new Vector2(.455f,1.015f),new Vector2(.545f,1.105f));
            Picture("Trophy laurel left",parent,theme.leftLaurels[0],new Vector2(.41f,1.01f),new Vector2(.465f,1.10f));
            Picture("Trophy laurel right",parent,theme.rightLaurels[0],new Vector2(.535f,1.01f),new Vector2(.59f,1.10f));
            statistics[0]=Stat("Race time",parent,.14f,theme.timeIcon,theme.timeWord);
            statistics[1]=Stat("Kills",parent,.39f,theme.punch,theme.killsWord);
            statistics[2]=Stat("Coins",parent,.64f,theme.coinIcon,theme.coinsWord);
            for(int i=0;i<3;i++)
            {
                int column=i==0?1:i==1?0:2;
                float x=.06f+column*.295f;
                var panel=Plate("Podium "+(i+1),parent,theme.podiumBanners[i],new Vector2(x,.555f),new Vector2(x+.29f,i==0?.745f:.715f));
                Picture("Medal",panel,theme.medals[i],new Vector2(.055f,.12f),new Vector2(.29f,.90f));
                Picture("Laurel left",panel,theme.leftLaurels[i],new Vector2(.02f,.15f),new Vector2(.10f,.76f));
                Picture("Laurel right",panel,theme.rightLaurels[i],new Vector2(.88f,.15f),new Vector2(.97f,.76f));
                Picture("Podium rank",panel,i==0?theme.first:i==1?theme.second:theme.third,new Vector2(.43f,.67f),new Vector2(.70f,.94f));
                podiumNames[i]=Label("Winner name",panel,new Vector2(.30f,.33f),new Vector2(.91f,.67f),30);
                podiumTimes[i]=Label("Winner time",panel,new Vector2(.30f,.05f),new Vector2(.91f,.36f),27);
            }
            var results=Group("Finish standings",parent,new Vector2(.065f,.19f),new Vector2(.935f,.535f));
            resultRows=MakeRows(results,true);
            returnText=Label("Return countdown",parent,new Vector2(.22f,.15f),new Vector2(.78f,.185f),20);
        }

        TMP_Text Stat(string title,Transform parent,float x,Sprite icon,Sprite word)
        {
            var root=Plate(title,parent,theme.statBar,new Vector2(x,.77f),new Vector2(x+.22f,.835f));
            Picture("Icon",root,icon,new Vector2(.035f,.10f),new Vector2(.17f,.90f));
            Picture("Label",root,word,new Vector2(.21f,.25f),new Vector2(.43f,.75f));
            return Label("Value",root,new Vector2(.45f,.10f),new Vector2(.96f,.90f),28);
        }

        Row[] MakeRows(Transform parent,bool twoColumns)
        {
            var rows=new Row[20];
            int perColumn=twoColumns?10:20;
            float height=.95f/perColumn;
            for(int i=0;i<rows.Length;i++)
            {
                int column=twoColumns?i/10:0, index=i%perColumn;
                float x=twoColumns?column*.515f:.025f;
                float width=twoColumns?.485f:.95f;
                float y=.02f+(perColumn-1-index)*height;
                var root=Plate("Racer "+(i+1),parent,theme.row,new Vector2(x,y),new Vector2(x+width,y+height*.96f));
                var row=new Row{Root=root,Background=root.GetComponent<UnityEngine.UI.Image>()};
                row.Rank=Label("Rank",root,new Vector2(.01f,0),new Vector2(.13f,1),twoColumns?25:23);
                row.Name=Label("Name",root,new Vector2(.15f,0),new Vector2(twoColumns?.46f:.59f,1),twoColumns?25:22);
                row.Name.alignment=TextAlignmentOptions.MidlineLeft;
                row.Value=Label(twoColumns?"Time":"Kills",root,new Vector2(twoColumns?.48f:.60f,0),new Vector2(twoColumns?.76f:.75f,1),twoColumns?23:21);
                row.Tail=Label(twoColumns?"Kills":"Lap",root,new Vector2(twoColumns?.79f:.76f,0),new Vector2(.99f,1),twoColumns?23:20);
                rows[i]=row;
            }
            return rows;
        }

        public void Draw(SkateCombat player,bool results,bool paused)
        {
            bool playing=!results&&!player.HasFinishedRace;
            healthRoot.gameObject.SetActive(playing);raceRoot.gameObject.SetActive(playing);
            standingsRoot.gameObject.SetActive(!results);
            for(int i=0;i<hearts.Length;i++)hearts[i].sprite=i<player.Health.Value?theme.fullHeart:theme.emptyHeart;
            healthCount.text=player.Health.Value+"/"+player.MaxHealth;
            placeText.text="<color=#FFDC3B>"+Mathf.Max(1,player.RacePlace.Value)+"</color>/"+SkateCombat.Players.Count;
            int lap=Mathf.Min(player.CompletedLaps.Value+1,rules.RequiredLaps);
            if(extraLap!=null)extraLap.text=lap+"/"+rules.RequiredLaps;
            double until=player.StartsAt.Value-(Unity.Netcode.NetworkManager.Singleton?.ServerTime.Time??0);
            bool counting=playing&&!paused&&player.StartsAt.Value>0&&until> -1;
            start.enabled=counting;
            if(counting)start.sprite=until>0?Digit(Mathf.Clamp(Mathf.CeilToInt((float)until),1,3)):theme.go;
            if(hud.timerText!=null)hud.timerText.alpha=counting||playing&&player.Started?0:1;
            if(hud.rankingText!=null)hud.rankingText.text="";
            if(boostEmpty!=null)boostEmpty.enabled=playing&&player.BoostReserve.Value<=0;
            if(emptyItem!=null)emptyItem.enabled=playing&&player.ItemCount.Value<=0;
            spectating.enabled=!results&&!paused&&player.HasFinishedRace&&!player.Finished.Value;
            var racers=SkateCombat.Players.Where(p=>p!=null&&p.IsSpawned).OrderBy(p=>p.RacePlace.Value<=0?int.MaxValue:p.RacePlace.Value).ThenBy(p=>p.NetworkObjectId).ToArray();
            AnimateStandings(racers,player);
        }

        void CacheStandingSlots()
        {
            if(standings==null)return;
            int count=Mathf.Min(VisibleStandings,standings.Length);
            standingSlotMin=new Vector2[count];standingSlotMax=new Vector2[count];
            for(int i=0;i<count;i++)
            {
                standingSlotMin[i]=standings[i].Root.anchorMin;
                standingSlotMax[i]=standings[i].Root.anchorMax;
            }
            standingsByRacer.Clear();
        }

        void AnimateStandings(SkateCombat[] racers,SkateCombat player)
        {
            if(standings==null||standingSlotMin==null||standingSlotMin.Length==0)CacheStandingSlots();
            if(standings==null||standingSlotMin==null)return;

            var racerIds=new HashSet<ulong>(racers.Select(r=>r.NetworkObjectId));
            foreach(var id in standingsByRacer.Keys.Where(id=>!racerIds.Contains(id)).ToArray())standingsByRacer.Remove(id);

            var usedRows=new HashSet<Row>(standingsByRacer.Values);
            for(int i=0;i<racers.Length&&i<standings.Length;i++)
            {
                ulong id=racers[i].NetworkObjectId;
                if(standingsByRacer.ContainsKey(id))continue;
                var free=standings.FirstOrDefault(row=>!usedRows.Contains(row));
                if(free==null)break;
                standingsByRacer[id]=free;usedRows.Add(free);
            }

            visibleStandingRows.Clear();
            int visibleCount=Mathf.Min(VisibleStandings,Mathf.Min(racers.Length,standingSlotMin.Length));
            float blend=1f-Mathf.Exp(-standingsMoveSpeed*Time.unscaledDeltaTime);
            for(int i=0;i<visibleCount;i++)
            {
                var racer=racers[i];
                if(!standingsByRacer.TryGetValue(racer.NetworkObjectId,out var row))continue;
                bool wasVisible=row.Root.gameObject.activeSelf;
                if(!wasVisible)row.Root.gameObject.SetActive(true);
                visibleStandingRows.Add(row);

                row.Root.anchorMin=wasVisible?Vector2.Lerp(row.Root.anchorMin,standingSlotMin[i],blend):standingSlotMin[i];
                row.Root.anchorMax=wasVisible?Vector2.Lerp(row.Root.anchorMax,standingSlotMax[i],blend):standingSlotMax[i];
                row.Root.anchoredPosition=Vector2.zero;row.Root.sizeDelta=Vector2.zero;
                row.Root.SetSiblingIndex(i);

                bool mine=racer==player;
                row.Background.sprite=mine?theme.selectedRow:theme.row;
                row.Rank.text=(racer.RacePlace.Value>0?racer.RacePlace.Value:i+1)+".";
                row.Rank.color=mine?new Color(.16f,.07f,.02f):new Color(1,.88f,.2f);
                row.Name.text=Clean(racer.Player.Username.Value.ToString()).ToUpperInvariant();
                row.Value.text=racer.Kills.Value+" K";
                row.Tail.text=racer.HasFinishedRace?"DONE":racer.Finished.Value?"DNF":"LAP "+Mathf.Min(racer.CompletedLaps.Value+1,rules.RequiredLaps)+"/"+rules.RequiredLaps;
            }
            foreach(var row in standings)
                if(!visibleStandingRows.Contains(row)&&row.Root.gameObject.activeSelf)row.Root.gameObject.SetActive(false);
        }

        public void Results(SkateCombat player,SkateRaceResult[] rows,int secondsToMenu)
        {
            if(finishRoot==null)return;
            int place=player.FinishPlace.Value;
            Sprite rank=place==1?theme.first:place==2?theme.second:place==3?theme.third:place==4?theme.fourth:null;
            finishRoot.gameObject.SetActive(player.HasFinishedRace);finishRank.sprite=rank;finishRank.enabled=rank!=null;
            rankText.text=rank==null?SkateRaceFeedback.Ordinal(place).ToUpperInvariant():"";
            hud.resultText.text=player.HasFinishedRace?"":"RACE COMPLETE · DID NOT FINISH";
            statistics[0].text=player.HasFinishedRace?SkateRaceFeedback.TimeLabel(player.FinishSeconds.Value):"—";
            statistics[1].text=player.Kills.Value.ToString();statistics[2].text="+"+player.CoinsEarned.Value;
            for(int i=0;i<3;i++)
            {
                var winner=rows.FirstOrDefault(r=>r.Finished&&r.Place==i+1);
                podiumNames[i].text=winner==null?"No finisher":winner.Name.ToUpperInvariant();
                podiumTimes[i].text=winner==null?"—":SkateRaceFeedback.TimeLabel(winner.Seconds);
            }
            for(int i=0;i<resultRows.Length;i++)
            {
                var row=resultRows[i];row.Root.gameObject.SetActive(i<rows.Length);if(i>=rows.Length)continue;
                var result=rows[i];row.Background.sprite=result.PlayerId==player.NetworkObjectId?theme.selectedRow:theme.row;
                row.Rank.text=result.Finished?result.Place.ToString("00"):"—";
                row.Name.text=result.Name.ToUpperInvariant();
                row.Value.text=result.Finished?SkateRaceFeedback.TimeLabel(result.Seconds):result.Disconnected?"LEFT":"DNF";
                row.Tail.text=result.Kills+" K";
            }
            returnText.text=secondsToMenu>=0?"MAIN MENU IN "+secondsToMenu+"s":"OTHER RACERS ARE STILL FINISHING";
        }

        static string Clean(string value)=>value.Replace('<','‹').Replace('>','›').Replace('\n',' ').Replace('\r',' ');
        Sprite Digit(int digit)=>theme.digits!=null&&digit>=0&&digit<theme.digits.Length?theme.digits[digit]:null;
        TMP_Text Label(string name,Transform parent,Vector2 min,Vector2 max,float size)
        {
            var label=Instantiate(hud.healthText,parent);label.name=name;label.gameObject.SetActive(true);
            if(textMaterial!=null)label.fontSharedMaterial=textMaterial;
            label.text="";label.richText=false;label.raycastTarget=false;label.color=Color.white;label.alpha=1;
            label.fontStyle=FontStyles.Bold;label.alignment=TextAlignmentOptions.Center;label.enableAutoSizing=true;
            label.fontSizeMin=10;label.fontSizeMax=size;label.textWrappingMode=TextWrappingModes.NoWrap;label.overflowMode=TextOverflowModes.Ellipsis;
            Anchor(label.rectTransform,min,max);return label;
        }
        static void Divider(Transform parent,float x)
        {
            var image=Picture("Divider",parent,null,new Vector2(x,.22f),new Vector2(x+.004f,.78f));
            image.color=new Color(.8f,.9f,1,.9f);
        }
        static RectTransform Plate(string name,Transform parent,Sprite sprite,Vector2 min,Vector2 max)
        {
            var image=Picture(name,parent,sprite,min,max);image.preserveAspect=false;Skin(image.transform,sprite);return image.rectTransform;
        }
        public static void Skin(Transform target,Sprite sprite)
        {
            if(target==null||sprite==null)return;
            var image=target.GetComponent<UnityEngine.UI.Image>();if(image==null)return;
            image.sprite=sprite;image.color=Color.white;
            image.type=sprite.border.sqrMagnitude>0?UnityEngine.UI.Image.Type.Sliced:UnityEngine.UI.Image.Type.Simple;
        }
        public static UnityEngine.UI.Image Picture(string name,Transform parent,Sprite sprite,Vector2 min,Vector2 max)
        {
            var root=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image));root.transform.SetParent(parent,false);
            var image=root.GetComponent<UnityEngine.UI.Image>();image.sprite=sprite;image.preserveAspect=true;image.raycastTarget=false;
            Anchor(image.rectTransform,min,max);return image;
        }
        static RectTransform Group(string name,Transform parent,Vector2 min,Vector2 max)
        {
            var root=new GameObject(name,typeof(RectTransform));root.transform.SetParent(parent,false);
            var rect=root.GetComponent<RectTransform>();Anchor(rect,min,max);return rect;
        }
        static void Anchor(RectTransform rect,Vector2 min,Vector2 max)
        {rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;rect.localScale=Vector3.one;}
    }
}

