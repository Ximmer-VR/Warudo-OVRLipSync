/*
 * OVRLipSyncNode.cs
 *
 * Description: This script defines the OVRLipSyncNode class, which is responsible for generating OVR Lip Sync animations for Warudo.
 *
 * Author: Ximmer
 * Date: February 17, 2024
 * Contact:
 *   - Email: support@ximmer.dev
 *   - Discord: @Ximmer
 *   - Twitch: https://www.twitch.tv/ximmer_vr
 *   - Bluesky: https://bsky.app/profile/ximmer.dev
 *   - Carrd: https://ximmer.carrd.co/
 *
 * Copyright (c) 2024 Ximmer
 *
 * MIT License
 * For the full license text, see the attached LICENSE file in the project root directory.
 */

using CSCore;
using CSCore.CoreAudioAPI;
using CSCore.SoundIn;
using CSCore.Streams;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Warudo.Core;
using Warudo.Core.Attributes;
using Warudo.Core.Data;
using Warudo.Core.Graphs;
using Warudo.Plugins.Core.Assets.Character;

namespace dev.ximmer.OVRLipSync
{

    [NodeType(Id = "6097b791-0b8d-469a-90f9-a67becacd91a", Title = "Generate OVR Lip Sync Animation", Category = "Procedural Animations")]
    public class OVRLipSyncNode : Node
    {
        const string nodeTitle = "Generate OVR Lip Sync Animation";

        [DataInput][Label("BlendShape List")] Dictionary<string, float> _blendshapeList = null;

        [DataInput]
        [Label("Character")]
        CharacterAsset _character = null;

        [DataInput]
        [AutoComplete(nameof(MicrophoneList))]
        [Label("Microphone")]
        private string _microphone = "";

        [DataInput]
        [FloatSlider(0.0f, 10.0f)]
        [Label("Gain")]
        public float _gain = 1.0f;

        [DataInput]
        [FloatSlider(-60.0f, 0.0f)]
        [Label("Noise Gate (db)")]
        public float _nosieGateDb = -40.0f;

        [DataInput]
        [FloatSlider(0.0f, 10.0f)]
        [Label("Hold Open (seconds)")]
        public float _holdOpen = 0.5f;

        [DataInput]
        [Label("Visemes")]
        [Disabled]
        VisemeData[] _visemes = null;

        public class VisemeData : StructuredData, ICollapsibleStructuredData
        {
            [DataInput]
            [Label("Viseme")]
            [Disabled]
            public Oculus.OVRLipSync.Viseme _viseme;

            [DataInput]
            [Label("BlendShape")]
            public string _shape = "";

            [DataInput]
            [Label("Weight")]
            [FloatSlider(0.0f, 2.0f)]
            public float _weight = 1.0f;

            // flag to prevent constantly searching for blendshapes every frame
            public bool _valid = false;

            public string GetHeader()
            {
                return _viseme.ToString() + ": " + _shape;
            }
            protected override void OnCreate()
            {
                base.OnCreate();

                Watch(nameof(_viseme), () =>
                {
                    _valid = false;
                });

                Watch(nameof(_shape), () =>
                {
                    _valid = false;
                });
            }
        }

        string _activeBinaryShape = "";

        [DataOutput]
        [Label("Output BlendShape List")]
        Dictionary<string, float> _outputBlendshapeList()
        {
            if (_visemes != null)
            {
                bool gateOpen = GateOpen();

                if (_binarize)
                {
                    int best = 0;
                    float best_value = 0.0f;
                    for (int i = 0; i < _visemes.Length; i++)
                    {
                        if (frame.Visemes[(int)_visemes[i]._viseme] > best_value)
                        {
                            best = i;
                            best_value = frame.Visemes[(int)_visemes[i]._viseme];
                        }
                    }

                    // Clear Shapes
                    for (int i = 0; i < _visemes.Length; i++)
                    {
                        UpdateShape(ref _visemes[i], 0.0f);
                    }

                    // Set Shapes
                    _activeBinaryShape = _automap[(Oculus.OVRLipSync.Viseme)best][2];
                    UpdateShape(ref _visemes[best], _visemes[best]._weight * 1.0f);
                }
                else
                {
                    for (int i = 0; i < _visemes.Length; i++)
                    {
                        if (gateOpen)
                        {
                            UpdateShape(ref _visemes[i], _visemes[i]._weight * frame.Visemes[(int)_visemes[i]._viseme]);
                        }
                        else
                        {
                            UpdateShape(ref _visemes[i], 0.0f);
                        }
                    }
                }

            }

            return _blendshapeList;
        }

        float _rms = 0.0f;

        [DataOutput]
        [Label("RMS Energy")]
        float rms()
        {
            return _rms;
        }

        [DataOutput]
        [Label("Output Db")]
        float db()
        {
            return RmsToDb(_rms);
        }

        [DataInput]
        [Label("Provider")]
        public Oculus.OVRLipSync.ContextProviders provider = Oculus.OVRLipSync.ContextProviders.Enhanced;

        [DataInput]
        [Label("Enable Acceleration")]
        public bool enableAcceleration = true;

        [DataInput]
        [IntegerSlider(1, 100)]
        [Label("Smoothing")]
        public int _smoothing = 70;

        [DataInput]
        [Label("Binarize")]
        bool _binarize = false;

        [DataInput]
        [Label("Show Debug")]
        bool _showDebug = false;

        [DataInput]
        [Markdown]
        [HiddenIf(nameof(_showDebug), Is.False)]
        public string _debugOutput = "";

        // Automap dictionary
        readonly Dictionary<Oculus.OVRLipSync.Viseme, List<string>> _automap = new Dictionary<Oculus.OVRLipSync.Viseme, List<string>>()
        {
            { Oculus.OVRLipSync.Viseme.SIL, new List<string>() { "vrc/sil", "vrc.v_sil", "sil", "" } },
            { Oculus.OVRLipSync.Viseme.PP,  new List<string>() { "vrc/pp",  "vrc.v_pp",  "pp",  "" } },
            { Oculus.OVRLipSync.Viseme.FF,  new List<string>() { "vrc/ff",  "vrc.v_ff",  "ff",  "" } },
            { Oculus.OVRLipSync.Viseme.TH,  new List<string>() { "vrc/th",  "vrc.v_th",  "th",  "" } },
            { Oculus.OVRLipSync.Viseme.DD,  new List<string>() { "vrc/dd",  "vrc.v_dd",  "dd",  "" } },
            { Oculus.OVRLipSync.Viseme.KK,  new List<string>() { "vrc/kk",  "vrc.v_kk",  "kk",  "" } },
            { Oculus.OVRLipSync.Viseme.CH,  new List<string>() { "vrc/ch",  "vrc.v_ch",  "ch",  "" } },
            { Oculus.OVRLipSync.Viseme.SS,  new List<string>() { "vrc/ss",  "vrc.v_ss",  "ss",  "" } },
            { Oculus.OVRLipSync.Viseme.NN,  new List<string>() { "vrc/nn",  "vrc.v_nn",  "nn",  "" } },
            { Oculus.OVRLipSync.Viseme.RR,  new List<string>() { "vrc/rr",  "vrc.v_rr",  "rr",  "" } },
            { Oculus.OVRLipSync.Viseme.AA,  new List<string>() { "vrc/aa",  "vrc.v_aa",  "aa",  "a" } },
            { Oculus.OVRLipSync.Viseme.E ,  new List<string>() { "vrc/e",   "vrc.v_e" ,  "e",   "e" } },
            { Oculus.OVRLipSync.Viseme.IH,  new List<string>() { "vrc/ih",  "vrc.v_ih",  "ih",  "i" } },
            { Oculus.OVRLipSync.Viseme.OH,  new List<string>() { "vrc/oh",  "vrc.v_oh",  "oh",  "o" } },
            { Oculus.OVRLipSync.Viseme.OU,  new List<string>() { "vrc/ou",  "vrc.v_ou",  "ou",  "u" } },
        };

        [FlowInput]
        public Continuation AutoMapVisemes()
        {
            if (_character == null)
            {
                Context.Service.Toast(Warudo.Core.Server.ToastSeverity.Error, "Automap Visemes", "Please select a character first.");
                return null;
            }

            _visemes = new VisemeData[0];

            foreach (KeyValuePair<Oculus.OVRLipSync.Viseme, List<string>> item in _automap)
            {
                string best = "";
                int bestDist = 200;
                bool found = false;
                foreach (KeyValuePair<string, List<string>> mesh in _character.BlendShapes)
                {
                    foreach (string shape in mesh.Value)
                    {
                        if (shape == "") continue;

                        foreach (string testShape in item.Value)
                        {
                            int dist = LevenshteinDistance(testShape.ToLower(), shape.ToLower());
                            Debug.Log(testShape + " " + shape + " " + dist.ToString());

                            if (dist == 0)
                            {
                                best = shape;
                                found = true;
                                break;
                            }

                            if (dist < bestDist)
                            {
                                best = shape;
                                bestDist = dist;
                            }
                        }
                        if (found) break;
                    }
                    if (found) break;
                }

                VisemeData vd = StructuredData.Create<VisemeData>();
                vd._viseme = item.Key;
                vd._shape = best;

                VisemeData[] nvd = new VisemeData[_visemes.Length + 1];
                Array.Copy(_visemes, nvd, _visemes.Length);
                _visemes = nvd;
                _visemes[_visemes.Length - 1] = vd;
            }

            BroadcastDataInput(nameof(_visemes));

            return null;
        }

        // ----------------------------------------
        // Internal Data
        // ----------------------------------------

        const int BUFFER_SIZE = 512;

        // cscore
        WasapiCapture _capture;
        SingleBlockNotificationStream _singleBlockNotificationStream = null;
        SoundInSource _soundInSource = null;
        IWaveSource _waveSource = null;

        float[] _audioBuffer = new float[BUFFER_SIZE];
        int _audioBufferIndex = 0;

        // ovrlipsync
        uint _context = 0;
        Oculus.OVRLipSync.Frame frame = new Oculus.OVRLipSync.Frame();

        public void UpdateShape(ref VisemeData vd, float value)
        {
            if (vd._shape == null) return;
            if (vd._shape == "") return;

            if (vd._valid)
            {
                _blendshapeList[vd._shape] = value;// frame.Visemes[(int)vd._viseme];
                return;
            }

            if (_character != null)
            {
                foreach (KeyValuePair<string, List<string>> mesh in _character.BlendShapes)
                {
                    if (mesh.Value.Contains(vd._shape))
                    {
                        vd._valid = true;
                        _blendshapeList[vd._shape] = value;// frame.Visemes[(int)vd._viseme];
                    }
                }
            }
        }

        public static int LevenshteinDistance(string source1, string source2)
        {
            int source1Length = source1.Length;
            int source2Length = source2.Length;

            int[,] matrix = new int[source1Length + 1, source2Length + 1];

            if (source1Length == 0)
                return source2Length;

            if (source2Length == 0)
                return source1Length;

            for (int i = 0; i <= source1Length; matrix[i, 0] = i++) { }
            for (int j = 0; j <= source2Length; matrix[0, j] = j++) { }

            for (int i = 1; i <= source1Length; i++)
            {
                for (int j = 1; j <= source2Length; j++)
                {
                    int cost = (source2[j - 1] == source1[i - 1]) ? 0 : 1;

                    matrix[i, j] = Math.Min(
                        Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                        matrix[i - 1, j - 1] + cost);
                }
            }

            return matrix[source1Length, source2Length];
        }

        public async UniTask<AutoCompleteList> MicrophoneList()
        {
            AutoCompleteEntry[] data = await Task.Run(() =>
            {

                using (var deviceEnumerator = new MMDeviceEnumerator())
                {
                    MMDeviceCollection devList = deviceEnumerator.EnumAudioEndpoints(DataFlow.Capture, DeviceState.Active);

                    AutoCompleteEntry[] list = new AutoCompleteEntry[devList.Count];

                    for (int i = 0; i < devList.Count; i++)
                    {
                        list[i] = new AutoCompleteEntry()
                        {
                            label = devList[i].FriendlyName,
                            value = devList[i].FriendlyName
                        };
                    }

                    return list;
                }
            });

            return AutoCompleteList.Single(data);
        }

        protected override void OnCreate()
        {
            base.OnCreate();


            if (_visemes == null || _visemes.Length == 0)
            {
                _visemes = new VisemeData[_automap.Count];

                for (int i = 0; i < _visemes.Length; i++)
                {
                    _visemes[i] = StructuredData.Create<VisemeData>();
                    _visemes[i]._viseme = (Oculus.OVRLipSync.Viseme)i;
                }
            }

            BroadcastDataInput(nameof(_visemes));


            if (Oculus.OVRLipSync.CreateContext(ref _context, provider, 0, enableAcceleration) != Oculus.OVRLipSync.Result.Success)
            {
                _context = 0;
                Context.Service.Toast(Warudo.Core.Server.ToastSeverity.Error, nodeTitle, "Error initializing OVRLipSync Context");
                return;
            }

            Watch(nameof(_smoothing), () =>
            {
                Oculus.OVRLipSync.Result result = Oculus.OVRLipSync.SendSignal(_context, Oculus.OVRLipSync.Signals.VisemeSmoothing, _smoothing, 0);

                if (result != Oculus.OVRLipSync.Result.Success)
                {
                    if (result == Oculus.OVRLipSync.Result.InvalidParam)
                    {
                        Context.Service.Toast(Warudo.Core.Server.ToastSeverity.Error, nodeTitle, "Invalid smoothing parameter");
                    }
                    else
                    {
                        Context.Service.Toast(Warudo.Core.Server.ToastSeverity.Error, nodeTitle, "Unexpected error setting smoothing parameter");
                    }
                }
            });

            Watch(nameof(provider), () =>
            {
                Oculus.OVRLipSync.DestroyContext(_context);
                if (Oculus.OVRLipSync.CreateContext(ref _context, provider, 0, enableAcceleration) != Oculus.OVRLipSync.Result.Success)
                {
                    _context = 0;
                    Context.Service.Toast(Warudo.Core.Server.ToastSeverity.Error, nodeTitle, "Error initializing OVRLipSync Context");
                    return;
                }
            });

            Watch(nameof(_microphone), () =>
            {
                if (_microphone == null) return;

                if (_capture != null)
                {
                    StopCapture();
                }

                using (var deviceEnumerator = new MMDeviceEnumerator())
                {
                    MMDeviceCollection devList = deviceEnumerator.EnumAudioEndpoints(DataFlow.Capture, DeviceState.Active);

                    for (int i = 0; i < devList.Count; i++)
                    {
                        if (_microphone == devList[i].FriendlyName)
                        {
                            _capture = new WasapiCapture();
                            _capture.Device = devList[i];
                            _capture.Initialize();
                            _soundInSource = new SoundInSource(_capture);
                            StartCapture();
                        }
                    }
                }

            });
        }

        public void StartCapture()
        {
            _capture.Start();
            _singleBlockNotificationStream = new SingleBlockNotificationStream(_soundInSource.ToSampleSource());
            _waveSource = _singleBlockNotificationStream.ToWaveSource();


            byte[] buffer = new byte[_waveSource.WaveFormat.BytesPerSecond / 2];

            _soundInSource.DataAvailable += (s, ea) =>
            {
                int read;
                while ((read = _waveSource.Read(buffer, 0, buffer.Length)) > 0)
                {
                }
            };

            _singleBlockNotificationStream.SingleBlockRead += SingleBlockNotificationStreamOnSingleBlockRead;
        }

        private float CalculateRms(float[] samples)
        {
            double sum = 0.0;
            for (int i = 0; i < samples.Length; i++)
                sum += samples[i] * samples[i];

            return (float)Math.Sqrt(sum / samples.Length);
        }

        private float RmsToDb(float rms)
        {
            const float minRms = 1e-9f; // avoid log(0)
            return 20.0f * (float)Math.Log10(Math.Max(rms, minRms));
        }

        float _gateOpenTime = 0.0f;

        private bool GateOpen()
        {
            float db = RmsToDb(_rms);

            if (db >= _nosieGateDb)
            {
                _gateOpenTime = _holdOpen;
                return true;
            }

            if (_gateOpenTime > 0.0f)
            {
                return true;
            }

            return false;
        }

        private void SingleBlockNotificationStreamOnSingleBlockRead(object sender, SingleBlockReadEventArgs e)
        {
            float level = e.Left + e.Right;

            _audioBuffer[_audioBufferIndex++] = level * _gain;
            if (_audioBufferIndex >= _audioBuffer.Length)
            {
                _audioBufferIndex = 0;

                _rms = CalculateRms(_audioBuffer);

                if (_context == 0 || Oculus.OVRLipSync.IsInitialized() != Oculus.OVRLipSync.Result.Success)
                {
                    return;
                }
                Oculus.OVRLipSync.ProcessFrame(_context, _audioBuffer, frame, false);
            }
        }

        public void StopCapture()
        {
            if (_singleBlockNotificationStream != null)
            {
                _singleBlockNotificationStream.SingleBlockRead -= SingleBlockNotificationStreamOnSingleBlockRead;
            }
            if (_capture != null)
            {
                _capture.Stop();
            }
        }

        protected override void OnDestroy()
        {
            if (_context != 0)
            {
                Oculus.OVRLipSync.DestroyContext(_context);
                _context = 0;
            }

            base.OnDestroy();
        }

        public override void OnUpdate()
        {
            if (_gateOpenTime > 0.0f)
            {
                _gateOpenTime -= Time.deltaTime;
            }
            else
            {
                _gateOpenTime = 0.0f;
            }

            if (_showDebug)
            {
                _debugOutput = "";
                _debugOutput += $"Noise Gate Open: {GateOpen()}\n\n";
                _debugOutput += $"Rms Db: {RmsToDb(_rms):00.0}\n\n";

                if (_binarize)
                {
                    _debugOutput += $"Active Viseme: {_activeBinaryShape}\n\n";
                }

                for (int i = 0; i < Enum.GetNames(typeof(Oculus.OVRLipSync.Viseme)).Length; i++)
                {
                    _debugOutput += Enum.GetName(typeof(Oculus.OVRLipSync.Viseme), i) + ": " + ((int)(frame.Visemes[i] * 100)).ToString() + "%\n\n";
                }
                BroadcastDataInput(nameof(_debugOutput));
            }
        }
    }
}