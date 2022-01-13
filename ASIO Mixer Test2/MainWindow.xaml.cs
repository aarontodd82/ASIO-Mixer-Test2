using NAudio.Dsp;
using NAudio.Utils;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ASIO_Mixer_Test2
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        #region Variables
        protected int SampleRate = 48000;
        protected ASIO ASIO;
        protected float[][] InputBuffer;
        protected float[][] OutputBuffer;

        protected BiQuadFilter[][] Filters;

        protected int NumberOf_IO_Channels = 8; //In and Out must be the same (for now)


        protected int InputChannelOffset = 0;
        protected int OutputChannelOffset = 0;
        protected int SamplesPerChannel = 32;
        protected int BufferSize = 32;
        protected float InputVolume = .95f;
        protected float OutputVolume = .95f;

        protected volatile bool IsMultiThreadingEnabled = false;
        protected readonly Thread DSP_Thread;
        protected readonly AutoResetEvent DSP_RunOnce_ARE = new AutoResetEvent(false);
        protected readonly AutoResetEvent DSP_PassCompleted_ARE = new AutoResetEvent(false);
        protected volatile bool DSP_AllowedToRun = true;

        protected DateTime DSP_StartTime;
        protected TimeSpan DSP_ProcessingTime;

        private readonly float[,] routingMatrix;



        #endregion



        public MainWindow()
        {
            InitializeComponent();

            foreach (var device in ASIO.GetDriverNames())
            {
                comboAsioDevices.Items.Add(device);
            }



            this.Filters = new BiQuadFilter[this.NumberOf_IO_Channels][];
            for (int i = 0; i < this.NumberOf_IO_Channels; i++)
            {
                this.Filters[i] = new BiQuadFilter[1];
                //this.Filters[i][0] = BiQuadFilter.LowPassFilter(this.SampleRate, 250f, 1);
                //this.Filters[i][1] = BiQuadFilter.PeakingEQ(this.SampleRate, 20, 1.0f, 0.3f);
                //this.Filters[i][2] = BiQuadFilter.PeakingEQ(this.SampleRate, 30, 1.0f, 0.3f);
                //this.Filters[i][3] = BiQuadFilter.PeakingEQ(this.SampleRate, 40, 1.0f, 0.3f);
                //this.Filters[i][4] = BiQuadFilter.PeakingEQ(this.SampleRate, 50, 1.0f, 0.3f);
                //this.Filters[i][5] = BiQuadFilter.PeakingEQ(this.SampleRate, 60, 1.0f, 0.3f);
                //this.Filters[i][6] = BiQuadFilter.PeakingEQ(this.SampleRate, 70, 1.0f, 0.3f);
                //this.Filters[i][7] = BiQuadFilter.PeakingEQ(this.SampleRate, 80, 1.0f, 0.3f);
                //this.Filters[i][8] = BiQuadFilter.PeakingEQ(this.SampleRate, 90, 1.0f, 0.3f);
                //this.Filters[i][9] = BiQuadFilter.PeakingEQ(this.SampleRate, 100, 1.0f, 0.3f);
            }

            routingMatrix = new float[8, 8];
            // initial routing is each input straight to the same number output
            for (int n = 0; n < Math.Min(8, 8); n++)
            {
                routingMatrix[n, n] = 1.0f;
            }

            //added to test
            routingMatrix[0, 0] = 1.0f;
            routingMatrix[1, 0] = 1.0f;
            routingMatrix[2, 0] = 1.0f;
            routingMatrix[3, 0] = 1.0f;
            //added to test


            this.DSP_Thread = new Thread(new ThreadStart(this.Threaded_DSP));
            this.DSP_Thread.Start();




        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            //ASIO uses unmanaged Windows OLE com sub-system, we have to dispose it
            this.ASIO?.Dispose();
            this.ASIO = null;

            //Gracefully ask the DSP Thread to exit
            this.DSP_AllowedToRun = false;
            this.DSP_RunOnce_ARE.Set();

            Thread.Sleep(50); //Give the DSP Thread time to exit gracefully
            if (this.DSP_Thread.IsAlive) //If it's still running at this point, we hard abort it
            {
                //we don't care about Thread errors, we are closing down
                try
                {
                    this.DSP_Thread.Abort();
                }
                catch (Exception ex)
                {
                    _ = ex;
                }
            }
        }

            private void OnButtonBeginClick(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Test");


           

            //Create ASIO Device
            if (this.ASIO == null)
            {
                this.ASIO = new ASIO((string)comboAsioDevices.SelectedItem);
                
                this.ASIO.Init(NumberOf_IO_Channels, SampleRate, OutputChannelOffset, InputChannelOffset);

                //Create the Input and Output buffers (default HW size * number of channels)
                var FramesPerBuffer = this.ASIO.SamplesPerBuffer;
                var BufferSize = FramesPerBuffer * NumberOf_IO_Channels;
                
                
                //For performance reasons, only create the array once!
                this.InputBuffer = new float[BufferSize][];
                for (int i = 0; i < this.InputBuffer.Length; i++)
                {
                    this.InputBuffer[i] = new float[SamplesPerChannel];
                }
                this.OutputBuffer = new float[BufferSize][];
                for (int i = 0; i < this.OutputBuffer.Length; i++)
                {
                    this.OutputBuffer[i] = new float[SamplesPerChannel];
                }


                this.ASIO.AudioAvailable += this.On_ASIO_AudioAvailable;


            }

            this.ASIO?.Start();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected virtual void On_ASIO_AudioAvailable(object sender, AsioAudioAvailableEventArgs e)
        {
            //Assumes InputBuffer and OutputBuffer are pre-initialized for performance reasons

           

            this.DSP_StartTime = DateTime.Now;

            //Get the ASIO input stream
            e.GetAsJaggedSamples(InputBuffer);


            //IDK?
            this.Dispatcher.Invoke(() =>
            {
                if (chk_Threading.IsChecked ?? true)
                {
                    this.IsMultiThreadingEnabled = true;

                }
                else
                {
                    this.IsMultiThreadingEnabled = false;

                }
            });
            //IDK?



            if (this.IsMultiThreadingEnabled) //this.IsMultiThreadingEnabled
            {
                this.DSP_RunOnce_ARE.Set(); //Run one pass of the DSP
                this.DSP_PassCompleted_ARE.WaitOne(); //Wait until the DSP is done
            }
            else
            {
                this.DSP();
            }

            //Send OutputBuffer to ASIO Output stream
            e.SetAsJaggedSamples(OutputBuffer);

            this.DSP_ProcessingTime = DateTime.Now - this.DSP_StartTime;
        }

        protected virtual void Threaded_DSP()
        {
            try
            {
                while (true) //Keep-alive
                {
                    this.DSP_RunOnce_ARE.WaitOne(); //Pause the thread until signaled
                    if (!this.DSP_AllowedToRun) //Check if we should run
                        break;

                    this.DSP_MultiThreaded();

                    this.DSP_RunOnce_ARE.Reset(); //Tell the thread it is ready to pause
                    this.DSP_PassCompleted_ARE.Set(); //Signal that we are done
                }
            }
            catch (Exception ex)
            {
                _ = ex;
            }
        }

        protected virtual void DSP_MultiThreaded()
        {
            try
            {
                var TaskList = new List<Task>();
                for (int ChannelIndex = 0; ChannelIndex < NumberOf_IO_Channels; ChannelIndex++)
                {
                    var tempChannelIndex = (int)ChannelIndex;
                    TaskList.Add(Task.Run(() =>
                    {
                        this.DSP_Process_Channel(tempChannelIndex);
                    }));
                }

                Task.WaitAll(TaskList.ToArray());
                TaskList.Clear();
            }
            catch (Exception ex)
            {
                _ = ex;
            }
        }

        protected virtual void DSP()
        {
            try
            {

                for (int OutChannelIndex = 0; OutChannelIndex < this.NumberOf_IO_Channels; OutChannelIndex++)
                {

                    this.DSP_Process_Channel(OutChannelIndex);




                }
            }
            catch (Exception ex)
            {
                _ = ex;
            }
        }

        protected virtual void DSP_Process_Channel(int OutChannelIndex)
        {
            var ChannelFilters = this.Filters[OutChannelIndex];
            var ChannelFilterCount = ChannelFilters.Length;



            for (int SampleIndex = 0; SampleIndex < SamplesPerChannel; SampleIndex++)
            {

                float TempInSample = 0;
                float TempOutSample = 0;
                int FilterIndex = 0;

                for (int InChannelIndex = 0; InChannelIndex < this.NumberOf_IO_Channels; InChannelIndex++)
                {


                    TempInSample = (float)InputBuffer[InChannelIndex][SampleIndex] * this.InputVolume;

                    //Filtering
                    for (; FilterIndex < ChannelFilterCount; FilterIndex++)
                    {
                        TempInSample = ChannelFilters[FilterIndex].Transform(TempInSample);
                    }
                    //Filtering

                    //Routing
                    var amount = routingMatrix[InChannelIndex, OutChannelIndex];
                    if (amount > 0)
                        TempOutSample = TempOutSample + TempInSample * amount;
                    //Routing

                }
                OutputBuffer[OutChannelIndex][SampleIndex] = TempOutSample * this.OutputVolume;


            }

        }

        private void chan1_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            routingMatrix[0, 0] = (float)chan1.Value;

            
        }
    }
}
