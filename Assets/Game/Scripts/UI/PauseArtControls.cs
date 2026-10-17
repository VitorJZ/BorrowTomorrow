using UnityEngine;
using UnityEngine.UI;
namespace BorrowTomorrow.UI
{
    public sealed class PauseArtControls : MonoBehaviour
    {
        public Button quit;
        private void Start(){if(quit!=null)quit.onClick.AddListener(()=>Application.Quit());}
    }
}
