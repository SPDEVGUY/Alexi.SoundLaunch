using IntelOrca.Launchpad;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SoundLaunch
{
    class Program
    {
        static void Main(string[] args)
        {

            using (var pad = new LaunchPad())
            using(var snd = new SoundRunner())
            {


                pad.OnLog += (p, t) => Console.WriteLine("PAD >" + t);
                snd.OnLog += (p, t) => Console.WriteLine("SND >" + t);

                pad.OnButtonChange += (p, b) =>
                {
                    if(b.Button.State == ButtonPressState.Down)
                    {
                        snd.PlayCue(b.ButtonId);
                    }
                    if (b.Button.State == ButtonPressState.Up && pad.IsButtonActive(ToolbarButton.Mixer))
                    {
                        snd.StopCue(b.ButtonId);
                    }
                };

                pad.Start();
                snd.BuildConfigFromDir("clips");

                foreach(var s in snd.ActiveConfig.Sounds)
                {
                    foreach(var c in s.Cues)
                    {
                        var b = pad.GetButton(c.CueName);
                        if(b != null)
                        {
                            var col = ButtonColor.Green;
                            if (c.PitchFactor < 1.0f) col = ButtonColor.GreenLow;
                            else if (c.PitchFactor > 1.0f) col = ButtonColor.GreenMid;
                            if (s.Loop) col = ButtonColor.Red;
                            pad.SetColor(b, col);
                        }
                    }
                }

                Console.ReadLine();

                pad.Stop();
            }
        }
    }
}
