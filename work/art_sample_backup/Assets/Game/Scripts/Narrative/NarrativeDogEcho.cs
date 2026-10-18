using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Core.Interfaces;
using UnityEngine;
namespace BorrowTomorrow.Narrative
{
    public sealed class NarrativeDogEcho : MonoBehaviour
    {
        public Texture2D sheet;
        public Shader cutoutShader;
        private Sprite[] frames;
        private SpriteRenderer renderer2d;
        private DialoguePlayer player;
        private Material material;
        private float echoTimer;
        private void Start()
        {
            renderer2d=gameObject.AddComponent<SpriteRenderer>();renderer2d.sortingLayerName="EraPlatform";renderer2d.sortingOrder=5;
            frames=new Sprite[16];
            for(int i=0;i<16;i++)frames[i]=Sprite.Create(sheet,new Rect((i%4)*sheet.width/4,(3-i/4)*sheet.height/4,sheet.width/4,sheet.height/4),new Vector2(.5f,.15f),200);
            if(cutoutShader!=null){material=new Material(cutoutShader);renderer2d.sharedMaterial=material;}
            if(ServiceRegistry.TryGet<IDialogueService>(out var d))player=d as DialoguePlayer;
            if(player!=null)player.LineStarted+=OnLine;
        }
        private void OnLine(DialogueEntry entry)
        {
            if(entry.entryId!="DLG_L00_DOG_01")return;
            echoTimer=4.5f;
            if(ServiceRegistry.TryGet<IAudioService>(out var audio))audio.PlaySfx("sfx_dog_soft");
        }
        private void Update()
        {
            if(renderer2d==null)return;
            if(GameSession.Instance!=null && GameSession.Instance.State.IsPaused)return;
            echoTimer=Mathf.Max(0,echoTimer-Time.deltaTime);
            bool past=ServiceRegistry.TryGet<IBoundaryContext>(out var b)&&b.EraAtAnchor(14)==Era.Past;
            renderer2d.enabled=past||echoTimer>0;
            int frame=echoTimer>0?8+((int)(Time.time*7)%8):0;
            renderer2d.sprite=frames[frame];
        }
        private void OnDestroy()
        {
            if(player!=null)player.LineStarted-=OnLine;
            if(frames!=null)foreach(var f in frames)if(f!=null)Destroy(f);
            if(material!=null)Destroy(material);
        }
    }
}
