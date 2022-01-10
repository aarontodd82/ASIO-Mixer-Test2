using System;
using NAudio.Wave.Asio;

namespace NAudio.Wave
{
    /// <summary>
    /// ASIO Out Player. New implementation using an internal C# binding.
    ///
    /// This implementation is only supporting Short16Bit and Float32Bit formats and is optimized
    /// for 2 outputs channels .
    /// SampleRate is supported only if AsioDriver is supporting it
    ///    
    /// This implementation is probably the first AsioDriver binding fully implemented in C#!
    ///
    /// Original Contributor: Mark Heath
    /// New Contributor to C# binding : Alexandre Mutel - email: alexandre_mutel at yahoo.fr
    /// Refactored by BassThatHz
    /// </summary>
    public class ASIO : IDisposable
    {
        #region Variables

        #region Object References
        protected readonly AsioDriver Driver;
        protected readonly AsioDriverExt DriverExt;
        protected readonly System.Threading.SynchronizationContext SyncContext;
        #endregion

        #endregion

        #region EventHandlers
        public event EventHandler<StoppedEventArgs> PlaybackStopped;
        public event EventHandler<AsioAudioAvailableEventArgs> AudioAvailable;
        public event EventHandler DriverResetRequest;
        #endregion

        #region Constructors, Dispose
        public ASIO(string driverName)
        {
            this.SyncContext = System.Threading.SynchronizationContext.Current;

            this.DriverName = driverName;
            this.Driver = AsioDriver.GetAsioDriverByName(this.DriverName);
            this.DriverExt = new AsioDriverExt(this.Driver)
            {
                ResetRequestCallback = this.OnDriverResetRequest
            };
        }

        public ASIO(int driverIndex)
        {
            this.SyncContext = System.Threading.SynchronizationContext.Current;

            String[] names = GetDriverNames();
            if (names.Length == 0)
                throw new ArgumentException("There is no ASIO Driver installed on your system");

            if (driverIndex < 0 || driverIndex > names.Length)
                throw new ArgumentException(String.Format("Invalid device number. Must be in the range [0,{0}]", names.Length));

            this.DriverName = names[driverIndex];
            this.Driver = AsioDriver.GetAsioDriverByName(this.DriverName);
            this.DriverExt = new AsioDriverExt(this.Driver)
            {
                ResetRequestCallback = this.OnDriverResetRequest
            };
        }

        public void Dispose()
        {
            if (this.PlaybackState != PlaybackState.Stopped)
                this.DriverExt?.Stop();

            this.DriverExt?.ReleaseDriver();
        }
        #endregion

        #region Public Static
        public static string[] GetDriverNames()
        {
            return AsioDriver.GetAsioDriverNames();
        }

        public static bool IsSupported()
        {
            return GetDriverNames().Length > 0;
        }
        #endregion

        #region Public Properties

        #region Read Only
        public virtual string DriverName { get; private set; }
        public virtual bool IsInitalized { get; private set; }
        public virtual PlaybackState PlaybackState { get; private set; }
        public virtual int NumberOfOutputChannels { get; private set; }
        public virtual int NumberOfInputChannels { get; private set; }
        public virtual int SamplesPerBuffer { get; private set; }
        #endregion

        public virtual bool AutoStop { get; set; }
        public virtual int OutputChannelOffset { get; set; }
        public virtual int InputChannelOffset { get; set; }
        #endregion

        #region Public Members

        public virtual void ShowControlPanel() => this.DriverExt.ShowControlPanel();

        public virtual bool IsSampleRateSupported(int sampleRate) =>
                                this.DriverExt.IsSampleRateSupported(sampleRate);
        public virtual int DriverInputChannelCount => this.DriverExt.Capabilities.NbInputChannels;

        public virtual int DriverOutputChannelCount => this.DriverExt.Capabilities.NbOutputChannels;

        public virtual string AsioInputChannelName(int channel) =>
                    channel > this.DriverInputChannelCount ?
                    "" :
                    this.DriverExt.Capabilities.InputChannelInfos[channel].name;


        public virtual string AsioOutputChannelName(int channel) =>
                    channel > this.DriverOutputChannelCount ?
                    "" :
                    this.DriverExt.Capabilities.OutputChannelInfos[channel].name;

        /// <summary>
        /// returns Tuple: int InputLatency, int OutputLatency
        /// </summary>
        public virtual Tuple<int, int> PlaybackLatency
        {
            get
            {
                this.DriverExt.Driver.GetLatencies(out int InputLatency, out int OutputLatency);
                return new Tuple<int, int>(InputLatency, OutputLatency);
            }
        }

        #endregion

        #region Public Functions
        public virtual void Init(int numberOfChannels, int desiredSampleRate, int outputChannelOffset, int inputChannelOffset)
        {
            if (this.IsInitalized)
                throw new InvalidOperationException("Already initialised this instance of Asio");
            this.IsInitalized = true;

            if (numberOfChannels <= 0)
                throw new InvalidOperationException("Invalid number of channels");

            this.NumberOfInputChannels = numberOfChannels;
            this.NumberOfOutputChannels = numberOfChannels;

            if (!this.DriverExt.IsSampleRateSupported(desiredSampleRate))
                throw new ArgumentException("SampleRate is not supported");

            if (this.DriverExt.Capabilities.SampleRate != desiredSampleRate)
                this.DriverExt.SetSampleRate(desiredSampleRate);

            // will throw an exception if channel offset is too high
            this.DriverExt.SetChannelOffset(outputChannelOffset, inputChannelOffset);

            // Plug the callback
            this.DriverExt.FillBufferCallback = this.On_Has_ASIO_Data;

            // Used Prefered size of ASIO Buffer
            this.SamplesPerBuffer = this.DriverExt.CreateBuffers(NumberOfOutputChannels, NumberOfInputChannels, false);
        }

        public virtual void Start()
        {
            if (this.PlaybackState != PlaybackState.Playing)
            {
                this.PlaybackState = PlaybackState.Playing;
                this.DriverExt.Start();
            }
        }

        public virtual void Stop()
        {
            this.PlaybackState = PlaybackState.Stopped;
            DriverExt.Stop();
            RaisePlaybackStopped(null);
        }

        #endregion

        #region Protected Functions

        protected virtual void OnDriverResetRequest()
        {
            this.DriverResetRequest?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void On_Has_ASIO_Data(IntPtr[] inputChannels, IntPtr[] outputChannels)
        {
            var args = new AsioAudioAvailableEventArgs
                        (
                            inputChannels,
                            outputChannels,
                            this.SamplesPerBuffer,
                            this.DriverExt.Capabilities.InputChannelInfos[0].type
                        );

            this.AudioAvailable?.Invoke(this, args);
        }

        protected void RaisePlaybackStopped(Exception e)
        {
            var handler = this.PlaybackStopped;
            if (handler != null)
            {
                if (this.SyncContext == null)
                {
                    handler(this, new StoppedEventArgs(e));
                }
                else
                {
                    this.SyncContext.Post(state => handler(this, new StoppedEventArgs(e)), null);
                }
            }
        }
        #endregion


    }
}
