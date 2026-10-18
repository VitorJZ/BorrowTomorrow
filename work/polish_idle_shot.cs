using UnityEngine;
using System.Collections;
IEnumerator Shot(){yield return new WaitForSeconds(.5f);ScreenCapture.CaptureScreenshot("Docs/ArtSample_2026-10-18/Polish/screenshots/L01B_exit_idle.png");yield return null;}
return Shot();
