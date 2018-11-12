using IntelOrca.Launchpad;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SoundLaunch
{
    public enum ButtonColor : int
    {
        Off,
        Green,
        GreenMid,
        GreenLow,
        Mix_LowLow,
        Mix_MediumLow,
        Mix_HighLow,
        Mix_HighMedium,
        Mix_HighHigh,
        Mix_MediumHigh,
        Mix_MediumMedium,
        Mix_LowMedium,
        Mix_LowHigh,
        Red,
        RedMid,
        RedLow
    };
    public class LaunchPadButtonEvent
    {
        public ButtonPressEventArgs Event;
        public LaunchpadButton Button;
        public ButtonColor Color;
        public string ButtonId;
    }
    public class ButtonState
    {
        public DateTime TimePressed;
        public LaunchpadButton Button;
        public LaunchPadButtonEvent LastEvent;
    }
    public class LaunchPad : IDisposable
    {
        LaunchpadDevice _device;

        public bool IsRunning { get; private set; }
        public string Error { get; private set; }
        public int DebouncLimitInMs
        {
            get { return _debounceLimitMs; }
            set
            {
                if (value > 0) _debounceLimitMs = value;
                else _debounceLimitMs = 1;
            }
        }


        public delegate void ButtonPressedEvent(LaunchPad pad, LaunchPadButtonEvent e);
        public delegate void LogEvent(LaunchPad pad, string text);

        private Thread _stateMonitor;
        private int _debounceLimitMs = 5;

        public event LogEvent OnLog;
        private void Log(string text)
        {
            OnLog?.Invoke(this, text);
        }

        /// <summary>
        /// Always fires for any button
        /// </summary>
        public event ButtonPressedEvent OnButtonChange;

        /// <summary>
        /// Only fires on grid buttons
        /// </summary>
        public event ButtonPressedEvent OnGridButtonChange;

        /// <summary>
        /// Only fires on side buttons
        /// </summary>
        public event ButtonPressedEvent OnSideButtonChange;

        /// <summary>
        /// Onl fires on toolbar buttons
        /// </summary>
        public event ButtonPressedEvent OnToolbarButtonChange;


        private List<ButtonState> _activeButtons = new List<ButtonState>();


        public LaunchPad()
        {
        }

        private void StateMonitorThread()
        {

            _device = new LaunchpadDevice();
            _device.DoubleBuffered = true;
            _device.Reset();
            _device.ButtonPressed += _device_ButtonPressed;
            _activeButtons = new List<ButtonState>();

            IsRunning = true;
            Log("Started.");


            while(IsRunning)
            {
                var active = GetActiveButtons();
                var removed = new List<ButtonState>();
                foreach (var b in active) if (b.Button.State != ButtonPressState.Down)
                    {
                        removed.Add(b);
                    }
                foreach (var b in removed) _device_ButtonPressed(this, b.LastEvent.Event);


                Thread.Sleep(_debounceLimitMs);
            }

            if (_device != null)
            {
                _device.ButtonPressed -= _device_ButtonPressed;
                _device.Reset();
                _activeButtons = new List<ButtonState>();
            }

            _device = null;
            IsRunning = false;
            Log("Stopped.");
            _stateMonitor = null;

        }
        private void _device_ButtonPressed(object sender, ButtonPressEventArgs e)
        {
            var evt = new LaunchPadButtonEvent
            {
                Event = e
            };
            var btnName = "Unknown";
            if (e.Type == ButtonType.Grid) { evt.Button = _device[e.X, e.Y]; btnName = $"Grid({e.X},{e.Y})"; }
            if (e.Type == ButtonType.Toolbar){ evt.Button = _device.GetButton(e.ToolbarButton); btnName = $"Toolbar({e.ToolbarButton})"; }
            if (e.Type == ButtonType.Side) { evt.Button = _device.GetButton(e.SidebarButton); btnName = $"Side({e.SidebarButton})"; }
            evt.Color = GetColor(evt.Button.RedBrightness, evt.Button.GreenBrightness);
            evt.ButtonId = btnName;

            Log($"Button Event ({evt.ButtonId}) > {evt.Button.State}");

            string activeButtonStateText =null;
            lock (_activeButtons)
            {
                var btn = _activeButtons.Find((x) => x.Button == evt.Button);
                if (evt.Button.State == ButtonPressState.Up && btn != null)
                {
                    _activeButtons.Remove(btn);
                    activeButtonStateText = $"Deactivated, held for {(DateTime.Now - btn.TimePressed).TotalMilliseconds} ms.";
                }
                if (evt.Button.State == ButtonPressState.Down && btn == null)
                {
                    var bs = new ButtonState
                    {
                        Button = evt.Button,
                        LastEvent = evt,
                        TimePressed = DateTime.Now
                    };
                    _activeButtons.Add(bs);

                    activeButtonStateText = "Activated.";
                } else if (evt.Button.State == ButtonPressState.Down && btn != null)
                {
                    btn.TimePressed = DateTime.Now;
                    btn.LastEvent = evt;
                    activeButtonStateText = "Activated Again?";
                }
            }
            if(activeButtonStateText != null)
                Log($"Button Activity ({evt.ButtonId}) > {activeButtonStateText}");

            OnButtonChange?.Invoke(this, evt);
            if (e.Type == ButtonType.Grid) OnGridButtonChange?.Invoke(this, evt);
            if (e.Type == ButtonType.Toolbar) OnToolbarButtonChange?.Invoke(this, evt);
            if (e.Type == ButtonType.Side) OnSideButtonChange?.Invoke(this, evt);
        }

        public List<ButtonState> GetActiveButtons()
        {
            List<ButtonState> result;
            lock (_activeButtons) result = _activeButtons.ToArray().ToList();
            return result;
        }

        public bool IsButtonActive(ToolbarButton button)
        {
            return (GetActiveButtons()).Any((b) => 
            b.LastEvent.Event.Type == ButtonType.Toolbar 
            && b.LastEvent.Event.ToolbarButton == button
            && b.Button.State == ButtonPressState.Down);

        }
        public bool IsButtonActive(SideButton button)
        {
            return (GetActiveButtons()).Any((b) => 
            b.LastEvent.Event.Type == ButtonType.Side 
            && b.LastEvent.Event.SidebarButton == button
            && b.Button.State == ButtonPressState.Down);

        }
        public bool IsButtonActive(int x, int y)
        {
            return (GetActiveButtons()).Any((b) => 
            b.LastEvent.Event.Type == ButtonType.Grid 
            && b.LastEvent.Event.X == x 
            && b.LastEvent.Event.Y == y
            && b.Button.State == ButtonPressState.Down);
        }

        public bool IsButtonActive(string id)
        {
            return (GetActiveButtons()).Any((b) => 
            b.LastEvent.ButtonId== id
            && b.Button.State == ButtonPressState.Down);
        }

        public LaunchpadButton GetButton(ToolbarButton button)
        {
            if (!IsRunning) return null;
            
            return _device.GetButton(button);
        }

        public LaunchpadButton GetButton(SideButton button)
        {
            if (!IsRunning) return null;

            return _device.GetButton(button);
        }

        public LaunchpadButton GetButton(int x, int y)
        {
            if (!IsRunning) return null;
            if (x > 7 || y > 7) return null;

            return _device[x,y];
        }

        public LaunchpadButton GetButton(string id)
        {
            if(id.StartsWith("Toolbar"))
            {
                var name = id.Substring("Toolbar(".Length);
                name = name.Substring(0, name.Length - 1);
                ToolbarButton button;
                if (Enum.TryParse(name, true, out button)) return _device.GetButton(button);
            }else if (id.StartsWith("Side"))
            {
                var name = id.Substring("Side(".Length);
                name = name.Substring(0, name.Length - 1);
                SideButton button;
                if (Enum.TryParse(name, true, out button)) return _device.GetButton(button);

            } else if (id.StartsWith("Grid"))
            {
                var xy = id.Substring("Side(".Length);
                var xyArray = xy.Substring(0, xy.Length - 1).Split(',');

                int x;
                int y;
                if(int.TryParse(xyArray[0],out x) && int.TryParse(xyArray[1], out y))
                {
                    return GetButton(x, y);
                }
            }
            return null;
        }

        public IEnumerable<LaunchpadButton> GetButtons()
        {
            if (!IsRunning) return null;
            return _device.Buttons;
        }


        public void SetColor(LaunchpadButton btn, ButtonColor color)
        {
            ButtonBrightness green = ButtonBrightness.Off;
            ButtonBrightness red = ButtonBrightness.Off;

            switch(color)
            {
                case ButtonColor.Off: break;
                case ButtonColor.Green: green = ButtonBrightness.Full; break;
                case ButtonColor.GreenMid: green = ButtonBrightness.Medium; break;
                case ButtonColor.GreenLow: green = ButtonBrightness.Low; break;
                case ButtonColor.Red: red = ButtonBrightness.Full; break;
                case ButtonColor.RedMid: red = ButtonBrightness.Medium; break;
                case ButtonColor.RedLow: red = ButtonBrightness.Low; break;
                case ButtonColor.Mix_HighHigh: red = ButtonBrightness.Full; green = ButtonBrightness.Full; break;
                case ButtonColor.Mix_HighMedium: red = ButtonBrightness.Full; green = ButtonBrightness.Medium; break;
                case ButtonColor.Mix_HighLow: red = ButtonBrightness.Full; green = ButtonBrightness.Low; break;
                case ButtonColor.Mix_MediumHigh: red = ButtonBrightness.Medium; green = ButtonBrightness.Full; break;
                case ButtonColor.Mix_MediumMedium: red = ButtonBrightness.Medium; green = ButtonBrightness.Medium; break;
                case ButtonColor.Mix_MediumLow: red = ButtonBrightness.Medium; green = ButtonBrightness.Low; break;
                case ButtonColor.Mix_LowHigh: red = ButtonBrightness.Low; green = ButtonBrightness.Full; break;
                case ButtonColor.Mix_LowMedium: red = ButtonBrightness.Low; green = ButtonBrightness.Medium; break;
                case ButtonColor.Mix_LowLow: red = ButtonBrightness.Low; green = ButtonBrightness.Low; break;
            }
            btn.SetBrightness(red, green);
        }

        public ButtonColor GetColor(ButtonBrightness red, ButtonBrightness green)
        {
            if (red == ButtonBrightness.Off) {
                if (green == ButtonBrightness.Full) return ButtonColor.Green;
                if (green == ButtonBrightness.Medium) return ButtonColor.GreenMid;
                if (green == ButtonBrightness.Low) return ButtonColor.GreenLow;
                if (green == ButtonBrightness.Off) return ButtonColor.Off;
            }
            else if (red == ButtonBrightness.Low)
            {
                if (green == ButtonBrightness.Full) return ButtonColor.Mix_LowHigh;
                if (green == ButtonBrightness.Medium) return ButtonColor.Mix_LowMedium;
                if (green == ButtonBrightness.Low) return ButtonColor.Mix_LowLow;
                if (green == ButtonBrightness.Off) return ButtonColor.RedLow;
            }
            else if (red == ButtonBrightness.Medium)
            {
                if (green == ButtonBrightness.Full) return ButtonColor.Mix_MediumHigh;
                if (green == ButtonBrightness.Medium) return ButtonColor.Mix_MediumMedium;
                if (green == ButtonBrightness.Low) return ButtonColor.Mix_MediumLow;
                if (green == ButtonBrightness.Off) return ButtonColor.RedMid;
            }
            else if (red == ButtonBrightness.Full)
            {
                if (green == ButtonBrightness.Full) return ButtonColor.Mix_HighHigh;
                if (green == ButtonBrightness.Medium) return ButtonColor.Mix_HighMedium;
                if (green == ButtonBrightness.Low) return ButtonColor.Mix_HighLow;
                if (green == ButtonBrightness.Off) return ButtonColor.Red;
            }
            return ButtonColor.Off;
        }


        public void Start()
        {
            if (IsRunning) return;
            try
            {                
                _stateMonitor = new Thread(StateMonitorThread);
                _stateMonitor.Name = "LaunchPad-StateMonitor";
                _stateMonitor.Start();
            }
            catch (Exception ex) {
                _stateMonitor = null;
                IsRunning = false;
                Error = ex.Message;
                Log(ex.Message);
            }
        }


        public void Stop()
        {
            if(IsRunning)
            {
                Log("Stopping...");
                IsRunning = false;
                while (_stateMonitor != null) { Thread.Sleep(10); }
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
