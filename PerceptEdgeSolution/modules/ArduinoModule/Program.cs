namespace ArduinoModule
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Runtime.Loader;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
      
    using System.IO.Ports;
    using System.Runtime.Loader;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Iot.Device.Arduino;
    using Iot.Device.Common;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Microsoft.Extensions.Logging;
    using System.Device.Gpio;
    using Newtonsoft.Json;
    using System.Collections.Generic;
    using System.Linq;

    class Program
    {
        static int counter;

        static SerialPort port;
        static ArduinoBoard board;
        static GpioController gpioController;
        // Use Pin 6
        const int gpio = 12;

        static void Main(string[] args)
        {
            Init().Wait();

            // Wait until the app unloads or is cancelled
            var cts = new CancellationTokenSource();
            AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
            Console.CancelKeyPress += (sender, cpe) => cts.Cancel();
            WhenCancelled(cts.Token).Wait();

            port?.Close();
            board?.Dispose();
        }

        /// <summary>
        /// Handles cleanup operations when app is cancelled or unloads
        /// </summary>
        public static Task WhenCancelled(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            return tcs.Task;
        }

        /// <summary>
        /// Initializes the ModuleClient and sets up the callback to receive
        /// messages containing temperature information
        /// </summary>
        static async Task Init()
        {
            MqttTransportSettings mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
            ITransportSettings[] settings = { mqttSetting };

            // Open a connection to the Edge runtime
            ModuleClient ioTHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
            await ioTHubModuleClient.OpenAsync();
            Console.WriteLine("IoT Hub module client initialized.");

            // Register callback to be called when a message is received by the module
            await ioTHubModuleClient.SetInputMessageHandlerAsync("input1", PipeMessage, ioTHubModuleClient);

            await Task.Delay(60000);

            //string portNames = "COM3";
            string portNames = "/dev/ttyACM0,/dev/ttyACM1";
            //string portNames = "/dev/ttyS3";
            string[] portNameList = portNames.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);

            //try to connect to each port and find arduino
            bool connected = false;
            foreach (string portName in portNameList)
            {
                // Create an instance of the Arduino board object.
                if (await ConnectToArduino(portName, 115200))
                {
                    connected = true;
                    break;
                }
            }
            
            if (!connected)
            {
                Console.WriteLine("Could not connect to Arduino");
                return;
            }
        }

        private static async Task<bool> ConnectToArduino(string portName, int baudRate)
        {
            port = new SerialPort(portName, baudRate);
            try
            {
                port.Open();
                board = new ArduinoBoard(port.BaseStream);
                Console.WriteLine("Connected to Arduino on port " + portName);
                await OpenGpio(board);                                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to connect to Arduino on port " + portName);
                Console.WriteLine(ex.Message);
                return false;
            }            
        }


        public static async Task OpenGpio(ArduinoBoard board)
        {            
            gpioController = board.CreateGpioController();

            // Opening GPIO2
            gpioController.OpenPin(gpio);
            gpioController.SetPinMode(gpio, PinMode.Output);

            Console.WriteLine("Buzzing GPIO12");
            await Buzz(gpio);
            await Buzz(gpio);
            await Buzz(gpio);
            await Buzz(gpio);
            await Buzz(gpio);            
        }

        private static async Task Buzz(int gpio)
        {
            if (gpioController == null)
                return;

            Console.WriteLine("Buzz");
            gpioController.Write(gpio, PinValue.High);
            //Thread.Sleep(500);
            await Task.Delay(500);
            gpioController.Write(gpio, PinValue.Low);
            await Task.Delay(500);
        }


        /// <summary>
        /// This method is called whenever the module is sent a message from the EdgeHub. 
        /// It just pipe the messages without any change.
        /// It prints all the incoming messages.
        /// </summary>
        static async Task<MessageResponse> PipeMessage(Message message, object userContext)
        {
            int counterValue = Interlocked.Increment(ref counter);

            var moduleClient = userContext as ModuleClient;
            if (moduleClient == null)
            {
                throw new InvalidOperationException("UserContext doesn't contain " + "expected values");
            }

            byte[] messageBytes = message.GetBytes();
            string messageString = Encoding.UTF8.GetString(messageBytes);
            Console.WriteLine($"Received message: {counterValue}, Body: [{messageString}]");

            if (!string.IsNullOrEmpty(messageString))
            {
                var messageBody = JsonConvert.DeserializeObject<MessageBody>(messageString);
                
                if (messageBody != null && messageBody.NEURAL_NETWORK != null && messageBody.NEURAL_NETWORK.Any())
                {
                    await Buzz(gpio);
                    
                    using (var pipeMessage = new Message(messageBytes))
                    {
                        foreach (var prop in message.Properties)
                        {
                            pipeMessage.Properties.Add(prop.Key, prop.Value);
                        }
                        await moduleClient.SendEventAsync("output1", pipeMessage);
                    
                        Console.WriteLine("Received message sent");
                    }
                }
            }
            return MessageResponse.Completed;
        }
    }

    public class NEURALNETWORK
    {
        public List<double> bbox { get; set; }
        public string label { get; set; }
        public string confidence { get; set; }
        public string timestamp { get; set; }
    }

    public class MessageBody
    {
        public List<NEURALNETWORK> NEURAL_NETWORK { get; set; }
    }
}
