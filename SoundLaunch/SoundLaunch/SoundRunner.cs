using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SoundLaunch
{
    [JsonObject(MemberSerialization.OptIn)]
    public class SoundConfig
    {
        [JsonProperty]
        public List<Sound> Sounds;

        public string SourceFileName;

        public SoundConfig()
        {
            Sounds = new List<Sound>();
        }
        public void Save()
        {
            if (string.IsNullOrEmpty(SourceFileName)) SourceFileName = "soundconfig.json";
            Save(SourceFileName);
        }
        public void Save(string fileName)
        {
            var cfg = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(fileName, cfg);
        }
        public static SoundConfig Load(string fileName)
        {
            var cfgStr = File.ReadAllText(fileName);
            var obj = JsonConvert.DeserializeObject<SoundConfig>(cfgStr);
            obj.SourceFileName = fileName;
            return obj;
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class Sound : IDisposable
    {
        [JsonProperty]
        public string Id;

        [JsonProperty]
        public string FilePath;
        
        [JsonProperty]
        public bool Loop;
        
        [JsonProperty]
        public List<SoundCue> Cues = new List<SoundCue>();

        /// <summary>
        /// If Looping, and this is false, it will exit loop.
        /// </summary>
        public bool Enabled;

        protected AudioFileReader FileReader;
        protected SmbPitchShiftingSampleProvider PitchControl;
        protected FadeInOutSampleProvider FadeControl;
        protected WaveOutEvent WaveOut;
        protected SoundCue ActiveCue;

        public void Play(SoundCue cue=null,bool wait =false)
        {
            Stop();

            lock (this)
            {
                Enabled = true;
                ActiveCue = cue;
                BuildWaveOut();
                WaveOut.Play();
                if (wait) while (WaveOut.PlaybackState == PlaybackState.Playing) Thread.Sleep(5);
            }
        }
        public void Stop()
        {
            lock (this)
            {
                if (WaveOut != null && WaveOut.PlaybackState == PlaybackState.Playing) WaveOut.Stop();

                Enabled = false;
            }
        }
        protected void BuildWaveOut()
        {
            WaveOut?.Dispose();
            FileReader?.Dispose();

            FileReader = new AudioFileReader(FilePath);
            FileReader.Position = (ActiveCue?.StartPosition) ?? 0;
            FadeControl = new FadeInOutSampleProvider(FileReader,ActiveCue?.StartSilent??false);
            if (ActiveCue != null && (ActiveCue?.FadeInTime) > 0) FadeControl.BeginFadeIn(ActiveCue?.FadeInTime ?? 0);
            //if (ActiveCue != null && (ActiveCue?.FadeOutTime) > 0) FadeControl.(ActiveCue?.FadeInTime ?? 0);
            //TODO: FadeOutMonitor?

            PitchControl = new SmbPitchShiftingSampleProvider(FadeControl);
            PitchControl.PitchFactor = ActiveCue?.PitchFactor ?? 1;
            WaveOut = new WaveOutEvent();
            WaveOut.Init(PitchControl);
        }

        protected void LoopMonitor(object wave, StoppedEventArgs e)
        {
            if (Loop && Enabled) Play(ActiveCue);
        }

        public void Dispose()
        {
            FileReader?.Dispose();
            WaveOut?.Dispose();
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class SoundCue
    {
        [JsonProperty]
        public string CueName;

        [JsonProperty]
        public long StartPosition;


        [JsonProperty]
        public long FadeInTime;

        [JsonProperty]
        public long FadeOutTime;

        [JsonProperty]
        public bool StartSilent;

        [JsonProperty]
        public float PitchFactor;

    }

    public class SoundRunner : IDisposable
    {
        public SoundConfig ActiveConfig = new SoundConfig();

        public delegate void LogEvent(SoundRunner obj, string text);
        public event LogEvent OnLog;
        private void Log(string text)
        {
            OnLog?.Invoke(this, text);
        }


        public void BuildConfigFromDir(string subDirName)
        {
            if (Directory.Exists(subDirName))
            {
                var cfg = $".\\{subDirName}\\{subDirName}.json";
                if (File.Exists(cfg)) Load(cfg);
                else
                {
                    DisposeAudioCache();
                    ActiveConfig = new SoundConfig();
                    ActiveConfig.SourceFileName = cfg;
                }

                var filesFound = Directory.GetFiles(Environment.CurrentDirectory + "\\" + subDirName, "*.*")
                    .Where(s => s.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) || s.EndsWith(".wav", StringComparison.OrdinalIgnoreCase));

                var y = 0;
                foreach (var f in filesFound)
                {
                    var localFile = f.ToLower().Replace(Environment.CurrentDirectory.ToLower(), ".");

                    if (!ActiveConfig.Sounds.Any((x) => x.FilePath.Equals(localFile, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        var s = new Sound
                        {
                            FilePath = f.ToLower().Replace(Environment.CurrentDirectory.ToLower(), "."),
                            Id = (new FileInfo(f)).Name,
                            Loop = false
                        };

                        for(var x = 0; x< 8;x++)
                        {
                            s.Cues.Add(
                                new SoundCue
                                {
                                    CueName = $"Grid({x},{y})",
                                    FadeInTime = 0,
                                    FadeOutTime = 0,
                                    PitchFactor = x == 0.0f ? 1.0f : ((x/7.0f) * 2.0f),
                                    StartPosition=0,
                                    StartSilent=false
                                }
                            );
                        }
                        
                        ActiveConfig.Sounds.Add(s);
                        y++;
                    }
                }

                ActiveConfig.Save();
            }
            else Log($"Director not found {subDirName}");
        }

        public void Load(string fileName)
        {
            if (File.Exists(fileName))
            {
                DisposeAudioCache();
                try
                {
                    ActiveConfig = SoundConfig.Load(fileName);
                } catch(Exception ex)
                {
                    Log($"Failed to load cfg {fileName}, reason: {ex.Message}");
                    ActiveConfig = new SoundConfig();
                }
            }
        }

        public void Play(Sound sound, SoundCue cue = null, bool wait = false)
        {
            Log($"Playing {sound.Id}.");
            sound.Play(cue, wait);
        }

        public void PlayCue(string cueName)
        {
            var sounds = ActiveConfig.Sounds.FindAll((x) => x.Cues.Any((c) => c.CueName.Equals(cueName)));
            foreach(var s in sounds)
            {
                var cues = s.Cues.FindAll((cue) => cue.CueName.Equals(cueName));
                foreach (var c in cues) Play(s, c);
            }
        }
        public void StopCue(string cueName)
        {
            var sounds = ActiveConfig.Sounds.FindAll((x) => x.Cues.Any((c) => c.CueName.Equals(cueName)));
            foreach (var s in sounds) Stop(s);
        }


        public void Stop(Sound sound)
        {
            Log($"Stopping {sound.Id}.");

            sound.Stop();
        }

        protected void DisposeAudioCache()
        {
            foreach (var sound in ActiveConfig.Sounds) sound.Dispose();

            Log("Audio cache disposed.");
        }

               
        public Sound Get(string id)
        {
            return ActiveConfig.Sounds.Find((x) => x.Id.Equals(id, StringComparison.InvariantCultureIgnoreCase));
        }
        public void Dispose()
        {
            DisposeAudioCache();
        }
    }
}
