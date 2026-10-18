using System.Linq;
using System.IO;
using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Interfaces;
using UnityEngine;
var p=ServiceRegistry.Get<IPlayerContext>();
var snapshot=new {boundary=ServiceRegistry.Get<IBoundaryContext>().CommittedBoundary,x=p.Position.x,y=p.Position.y,flags=GameSession.Instance.StoryFlags.AllFlags.ToArray()};
File.WriteAllText("work/l00_fix_20261018/review_state.json",Codely.Newtonsoft.Json.JsonConvert.SerializeObject(snapshot));
return "Captured current L00 position and progress for restoration";
