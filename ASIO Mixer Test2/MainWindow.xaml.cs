using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        protected ASIO ASIO;
        protected float[] InputBuffer;
        protected float[] OutputBuffer;
        private bool running;


        #endregion

        public MainWindow()
        {
            InitializeComponent();

            foreach (var device in ASIO.GetDriverNames())
            {
                comboAsioDevices.Items.Add(device);
            }



           // this.CleanUp_ASIO();

       
            

            this.ASIO?.Start();

        }

        private void OnButtonBeginClick(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Test");

            int SampleRate = 96000;
            int NumberOf_IO_Channels = 4; //In and Out must be the same (for now)
            var InputChannelOffset = 0;
            var OutputChannelOffset = 2; //Push channel In 0,1 to Out 2,3

            //Create ASIO Device
            if (this.ASIO == null)
            {
                this.ASIO = new ASIO((string)comboAsioDevices.SelectedItem);
                this.ASIO.AudioAvailable += this.On_ASIO_AudioAvailable;
                this.ASIO.Init(NumberOf_IO_Channels, SampleRate, OutputChannelOffset, InputChannelOffset);

                //Create the Input and Output buffers (default HW size * number of channels)
                var FramesPerBuffer = this.ASIO.SamplesPerBuffer;
                var BufferSize = FramesPerBuffer * NumberOf_IO_Channels;
                //For performance reasons, only create the array once!
                this.InputBuffer = new float[BufferSize];
                this.OutputBuffer = new float[BufferSize];
            }
        }

        protected virtual void On_ASIO_AudioAvailable(object sender, AsioAudioAvailableEventArgs e)
        {
            //Assumes InputBuffer and OutputBuffer are pre-initialized for performance reasons

            //Get the Input stream
            e.GetAsInterleavedSamples(InputBuffer);

            #region Process the signal
            OutputBuffer = InputBuffer; //Map the Input to the Output

            //for (int i = 0; i < InputBuffer.Count(); i++)
            //{
            //    if (i + 2 < OutputBuffer.Length)
            //    {
            //        OutputBuffer[i + 2] = InputBuffer[i];
            //    }
            //}

            #endregion

            //set the Output stream
            e.SetAsInterleavedSamples(OutputBuffer);

            //Legacy Support, Not used
            e.WrittenToOutputBuffers = true;
        }

        private void CleanUp_ASIO()
        {
            ASIO.Stop();
            ASIO.Dispose();
            ASIO = null;
            running = false;
            buttonBegin.Content = "Begin";
        }


    }
}
