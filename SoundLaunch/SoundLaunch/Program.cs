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
                    if (!(
                        b.Event.Type == ButtonType.Toolbar &&
                        b.Event.ToolbarButton == ToolbarButton.Mixer
                        )
                    )
                    {
                        var isMixerHeld = pad.IsButtonActive(ToolbarButton.Mixer);
                        p.SetColor(b.Button, isMixerHeld ? ButtonColor.Red : ButtonColor.Green);
                    }

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


                Console.ReadLine();

                pad.Stop();
            }
        }
    }
}
