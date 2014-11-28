﻿using System;
using System.Threading;
using System.Diagnostics;

using NAudio.Wave;

using SharpTox.Av;
using SharpTox.Av.Filter;
using SharpTox.Core;

namespace Toxy.ToxHelpers
{
    class ToxGroupCall : ToxCall
    {
        public int GroupNumber;

        public ToxGroupCall(ToxAv toxav, int groupNumber)
            : base(toxav)
        {
            GroupNumber = groupNumber;
        }

        public override void Start(int input, int output, ToxAvCodecSettings settings)
        {
            WaveFormat outFormat = new WaveFormat((int)settings.AudioSampleRate, 2);
            WaveFormat outFormatSingle = new WaveFormat((int)settings.AudioSampleRate, 1);

            filterAudio = new FilterAudio((int)settings.AudioSampleRate);

            wave_provider = new BufferedWaveProvider(outFormat);
            wave_provider.DiscardOnBufferOverflow = true;
            wave_provider_single = new BufferedWaveProvider(outFormatSingle);
            wave_provider_single.DiscardOnBufferOverflow = true;

            if (WaveIn.DeviceCount > 0)
            {
                wave_source = new WaveIn();

                if (input != -1)
                    wave_source.DeviceNumber = input - 1;

                WaveFormat inFormat = new WaveFormat((int)ToxAv.DefaultCodecSettings.AudioSampleRate, wave_source.WaveFormat.Channels);

                wave_source.WaveFormat = inFormat;
                wave_source.DataAvailable += wave_source_DataAvailable;
                wave_source.RecordingStopped += wave_source_RecordingStopped;
                wave_source.BufferMilliseconds = ToxAv.DefaultCodecSettings.AudioFrameDuration;
                wave_source.StartRecording();
            }

            if (WaveOut.DeviceCount > 0)
            {
                wave_out = new WaveOut();

                if (output != -1)
                    wave_out.DeviceNumber = output - 1;

                wave_out.Init(wave_provider);
                wave_out.Play();

                wave_out_single = new WaveOut();

                if (output != -1)
                    wave_out_single.DeviceNumber = output - 1;

                wave_out_single.Init(wave_provider_single);
                wave_out_single.Play();
            }
        }

        public override void Stop()
        {
            if (wave_source != null)
            {
                wave_source.StopRecording();
                wave_source.Dispose();
            }

            if (wave_out != null)
            {
                wave_out.Stop();
                wave_out.Dispose();
            }

            if (timer != null)
                timer.Dispose();
        }

        protected override void wave_source_DataAvailable(object sender, WaveInEventArgs e)
        {
            short[] shorts = BytesToShorts(e.Buffer);

            if (filterAudio != null && FilterAudio)
                if (!filterAudio.Filter(shorts, shorts.Length / wave_source.WaveFormat.Channels))
                    Debug.WriteLine("Could not filter audio");

            if (!toxav.GroupSendAudio(GroupNumber, shorts, ((int)ToxAv.DefaultCodecSettings.AudioFrameDuration * (int)wave_source.WaveFormat.SampleRate) / 1000, wave_source.WaveFormat.Channels, wave_source.WaveFormat.SampleRate))
                Debug.WriteLine("Could not send audio to groupchat #{0}", GroupNumber);
        }

        /// <summary>
        /// Not implemented.
        /// </summary>
        public override void Call(int current_number, ToxAvCodecSettings settings, int ringing_seconds)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Not implemented.
        /// </summary>
        public override void Answer()
        {
            throw new NotImplementedException();
        }
    }

    class ToxCall
    {
        protected ToxAv toxav;
        protected FilterAudio filterAudio;

        protected WaveIn wave_source;
        protected WaveOut wave_out;
        protected WaveOut wave_out_single;
        protected BufferedWaveProvider wave_provider;
        protected BufferedWaveProvider wave_provider_single;

        public bool FilterAudio = false;

        protected Timer timer;
        public int TotalSeconds = 0;

        public int CallIndex;
        public int FriendNumber;

        public ToxCall(ToxAv toxav, int callindex, int friendnumber)
        {
            this.toxav = toxav;
            this.FriendNumber = friendnumber;

            CallIndex = callindex;
        }

        /// <summary>
        /// Dummy. Don't use this.
        /// </summary>
        /// <param name="toxav"></param>
        public ToxCall(ToxAv toxav)
        {
            this.toxav = toxav;
        }

        public virtual void Start(int input, int output, ToxAvCodecSettings settings)
        {
            toxav.PrepareTransmission(CallIndex, false);

            WaveFormat outFormat = new WaveFormat((int)settings.AudioSampleRate, (int)settings.AudioChannels);
            WaveFormat outFormatSingle = new WaveFormat((int)settings.AudioSampleRate, 1);

            wave_provider = new BufferedWaveProvider(outFormat);
            wave_provider.DiscardOnBufferOverflow = true;
            wave_provider_single = new BufferedWaveProvider(outFormatSingle);
            wave_provider_single.DiscardOnBufferOverflow = true;

            filterAudio = new FilterAudio((int)settings.AudioSampleRate);

            if (WaveIn.DeviceCount > 0)
            {
                wave_source = new WaveIn();

                if (input != -1)
                    wave_source.DeviceNumber = input - 1;

                WaveFormat inFormat = new WaveFormat((int)ToxAv.DefaultCodecSettings.AudioSampleRate, wave_source.WaveFormat.Channels);

                wave_source.WaveFormat = inFormat;
                wave_source.DataAvailable += wave_source_DataAvailable;
                wave_source.RecordingStopped += wave_source_RecordingStopped;
                wave_source.BufferMilliseconds = ToxAv.DefaultCodecSettings.AudioFrameDuration;
                wave_source.StartRecording();
            }

            if (WaveOut.DeviceCount > 0)
            {
                wave_out = new WaveOut();

                if (output != -1)
                    wave_out.DeviceNumber = output - 1;

                wave_out.Init(wave_provider);
                wave_out.Play();

                wave_out_single = new WaveOut();

                if (output != -1)
                    wave_out_single.DeviceNumber = output - 1;

                wave_out_single.Init(wave_provider_single);
                wave_out_single.Play();
            }
        }

        public virtual void SetTimerCallback(TimerCallback callback)
        {
            timer = new Timer(callback, null, 0, 1000);
        }

        protected void wave_source_RecordingStopped(object sender, StoppedEventArgs e)
        {
            Debug.WriteLine("Recording stopped");
        }

        public virtual void ProcessAudioFrame(short[] frame, int channels)
        {
            var waveOut = channels == 2 ? wave_out : wave_out_single;
            var waveProvider = channels == 2 ? wave_provider : wave_provider_single;

            if (waveOut != null && waveProvider != null)
            {
                byte[] bytes = ShortArrayToByteArray(frame);
                waveProvider.AddSamples(bytes, 0, bytes.Length);
            }
        }

        protected byte[] ShortArrayToByteArray(short[] shorts)
        {
            byte[] bytes = new byte[shorts.Length * 2];

            for (int i = 0; i < shorts.Length; ++i)
            {
                bytes[2 * i] = (byte)shorts[i];
                bytes[2 * i + 1] = (byte)(shorts[i] >> 8);
            }

            return bytes;
        }

        protected short[] BytesToShorts(byte[] buffer)
        {
            short[] shorts = new short[buffer.Length / 2];
            int index = 0;

            for (int i = 0; i < buffer.Length; i += 2)
            {
                byte[] bytes = new byte[] { buffer[i], buffer[i + 1] };
                shorts[index] = BitConverter.ToInt16(bytes, 0);

                index++;
            }

            return shorts;
        }

        protected virtual void wave_source_DataAvailable(object sender, WaveInEventArgs e)
        {
            short[] shorts = BytesToShorts(e.Buffer);

            if (filterAudio != null && FilterAudio)
                if (!filterAudio.Filter(shorts, shorts.Length / wave_source.WaveFormat.Channels))
                    Debug.WriteLine("Could not filter audio");

            byte[] dest = new byte[(((int)ToxAv.DefaultCodecSettings.AudioFrameDuration * wave_source.WaveFormat.SampleRate) / 1000) * 2 * wave_source.WaveFormat.Channels];
            int size = toxav.PrepareAudioFrame(CallIndex, dest, dest.Length, shorts, ((int)ToxAv.DefaultCodecSettings.AudioFrameDuration * (int)wave_source.WaveFormat.SampleRate) / 1000);

            ToxAvError error = toxav.SendAudio(CallIndex, dest, size);
            if (error != ToxAvError.None)
                Debug.WriteLine(string.Format("Could not send audio: {0}", error));
        }

        public virtual void Stop()
        {
            //TODO: we might want to block here until RecordingStopped and PlaybackStopped are fired

            if (wave_source != null)
            {
                wave_source.StopRecording();
                wave_source.Dispose();
            }

            if (wave_out != null)
            {
                wave_out.Stop();
                wave_out.Dispose();
            }

            toxav.KillTransmission(CallIndex);
            toxav.Hangup(CallIndex);

            if (timer != null)
                timer.Dispose();
        }

        public virtual void Answer()
        {
            ToxAvError error = toxav.Answer(CallIndex, ToxAv.DefaultCodecSettings);
            if (error != ToxAvError.None)
                throw new Exception("Could not answer call " + error.ToString());
        }

        public virtual void Call(int current_number, ToxAvCodecSettings settings, int ringing_seconds)
        {
            toxav.Call(current_number, settings, ringing_seconds, out CallIndex);
        }
    }
}
