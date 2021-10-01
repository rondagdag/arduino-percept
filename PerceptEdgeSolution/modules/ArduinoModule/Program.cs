namespace ArduinoModule
{
    using System;
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
            return;

            //string portNames = "COM3";
            string portNames = "/dev/ttyACM0,/dev/ttyACM1";
            //string portNames = "/dev/ttyS3";
            string[] portNameList = portNames.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);

            //try to connect to each port and find arduino
            bool connected = false;
            foreach (string portName in portNameList)
            {
                // Create an instance of the Arduino board object.
                if (ConnectToArduino(portName, 115200))
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

        private static bool ConnectToArduino(string portName, int baudRate)
        {
            using (var port = new SerialPort(portName, baudRate))
            {
                Console.WriteLine($"Connecting to Arduino on {portName}");
                try
                {
                    port.Open();
                }
                catch (UnauthorizedAccessException x)
                {
                    Console.WriteLine($"Could not open COM port: {x.Message} Possible reason: Arduino IDE connected or serial console open");
                    return false;
                }

                board = new ArduinoBoard(port.BaseStream);
                try
                {
                    // This implicitly connects
                    Console.WriteLine($"Connecting... Firmware version: {board.FirmwareVersion}, Builder: {board.FirmwareName}");
                    //while (Menu(board))
                    // TestGpio(board).Wait();
                    // Buzz(gpio).Wait();
                    // Buzz(gpio).Wait();
                    // Buzz(gpio).Wait();
                    //return true;

                    //while (true)
                    //{

                    Init().Wait();

                    // Wait until the app unloads or is cancelled
                    var cts = new CancellationTokenSource();
                    AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
                    Console.CancelKeyPress += (sender, cpe) => cts.Cancel();
                    WhenCancelled(cts.Token).Wait();
                    return true;

                    //}
                }
                catch (TimeoutException x)
                {
                    Console.WriteLine($"No answer from board: {x.Message} ");
                }
                finally
                {
                    port.Close();
                    board?.Dispose();
                }
            }
            return false;
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
            Console.WriteLine("Initializing IoT Hub module client");
            MqttTransportSettings mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
            ITransportSettings[] settings = { mqttSetting };

            // Open a connection to the Edge runtime
            ModuleClient ioTHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
            await ioTHubModuleClient.OpenAsync();
            Console.WriteLine("IoT Hub module client initialized.");

            //await TestGpio(board);
            
            // Register callback to be called when a message is received by the module
            await ioTHubModuleClient.SetInputMessageHandlerAsync("input1", PipeMessage, ioTHubModuleClient);            
            
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
                // Get the body of the message and deserialize it.
                var messageBody = JsonConvert.DeserializeObject<MessageBody>(messageString);
                
                if (messageBody != null && messageBody.NEURAL_NETWORK != null && messageBody.NEURAL_NETWORK.Any())
                {
                    //await Buzz(gpio);
                    
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

        public static async Task TestGpio(ArduinoBoard board)
        {            
            gpioController = board.CreateGpioController();

            // Opening GPIO2
            gpioController.OpenPin(gpio);
            gpioController.SetPinMode(gpio, PinMode.Output);

            Console.WriteLine("Buzzing GPIO12");
            //while (!Console.KeyAvailable)
            //while (true)
            //{
                await Buzz(gpio);
                await Buzz(gpio);
                await Buzz(gpio);
                await Buzz(gpio);
                await Buzz(gpio);
                //Thread.Sleep(500);
            //}

            //Console.ReadKey();
            //gpioController.Dispose();
        }

        private static async Task Buzz(int gpio)
        {
            Console.WriteLine("Buzz");
            gpioController.Write(gpio, PinValue.High);
            //Thread.Sleep(500);
            await Task.Delay(500);
            gpioController.Write(gpio, PinValue.Low);
            await Task.Delay(500);
        }
    }

        //Define the expected schema for the body of incoming messages.
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
