using System.IO.Ports;
using Chetch.Messaging;
using Chetch.Arduino;
using Chetch.Utilities;
using Chetch.Arduino.Devices;
using Chetch.Arduino.Devices.Displays;
using System.Threading.Tasks;

namespace ArduinoTest;

class Program
{
    const int BAUDRATE = 9600;
    const string PATH2DEVICE = "/dev/tty.usb*";
    //const string PATH2DEVICE = "/dev/cu.usb*";

    const byte OLED_DEVICE_ID = 10;
    const byte RECEIVER_ID = 11;
    const byte LGINSIDE_ID = 12;
    const byte LGOUTSIDE_ID = 13;


    static OLEDTextDisplay oled = new OLEDTextDisplay(OLED_DEVICE_ID, "oled");
    static IRTransmitter lgInside = new IRTransmitter(LGINSIDE_ID, "inside");

    static void printStatus(ArduinoBoard board)
    {
        Console.WriteLine("Board millis: {0}", board.Millis);
        Console.WriteLine("Devices: {0}", board.DeviceCount);
        Console.WriteLine("Free memory: {0}", board.FreeMemory);
    }


    static int n = 0;
    static async Task<int> DoCountAsync(bool thro)
    {
        if(thro)throw new Exception("wow");
        
        Task t = Task.Run(() =>{
            n = 0;
            for (int i = 1; i <= 5; i++)
            {
                n = i;
                //Console.WriteLine("Count: {0}", n);
                Thread.Sleep(500);
            }
            
        });
        
        await t;
        return n;
    }

    
    static async Task<Dictionary<String, IRData>> GetDeviceCommands(String deviceName)
    {
        String filename = "commands-" + deviceName.ToLower().Replace(" ", "-") + ".json";
        String content = String.Empty;
        if (File.Exists(filename))
        {
            content = File.ReadAllText(filename);
        }
        else
        {
            String uri = String.Format("http://localhost:8008/api/device-commands?device={0}", deviceName);
            using (HttpClient client = new HttpClient())
            {
                var response = await client.GetAsync(uri);
                content = await response.Content.ReadAsStringAsync();
            }
            if (!String.IsNullOrEmpty(content))
            {
                File.WriteAllText(filename, content);
            }
        }
        var result = JsonSerializer.Deserialize<List<IRData>>(content);
        Dictionary<String, IRData> commands = result.ToDictionary(x => x.CommandAlias, x => x);
        return commands;
    }


    static async Task Main(string[] args)
    {
        //ConsoleHelper.PK("Press a key to start");
       
        //foo();

        //ArduinoBoard board = new ArduinoBoard(0x0043, 9600, Frame.FrameSchema.SMALL_NO_CHECKSUM);
        ArduinoBoard board = new ArduinoBoard("first", 0x7523, 9600); //, Frame.FrameSchema.SMALL_NO_CHECKSUM);
        board.Ready += (sender, ready) => {
            Console.WriteLine("Board is ready: {0}", ready);
            if (ready) printStatus(board);
        };

        board.ErrorReceived += (sender, eargs) => {
            Console.WriteLine("{0} resulted in an error {1}", sender, eargs.Error);
            if(eargs.Error == ArduinoBoard.ErrorCode.DEVICE_ERROR && eargs.ErrorSource != null)
            {
                Console.WriteLine("{0} resulted in an error {1}", sender, eargs.Error);
            }
        };

        var ticker = new Ticker(10, "testDevice01");
        ticker.Updated += (sender, props) =>{
            Console.WriteLine("Ticker count is: {0}", ticker.Count);
        };
        //board.AddDevice(ticker);
        
        var switchDevice = new SwitchDevice(11, "glob");
        switchDevice.Switched += (sender, pinState) => {
            Console.WriteLine("{0} has pin state {1}", switchDevice.Name, switchDevice.PinState);
        };
        board.AddDevice(switchDevice);

        //ConsoleHelper.PK("Press a key to begin");
        
        await Task.Run(()=>{
            try{
                board.Begin();
            } catch (Exception e)
            {
                Console.WriteLine("FucK: {0}", e.Message);
            }

            Thread.Sleep(1000); 
            while(!board.IsReady)
            {
                Console.WriteLine("Waiting for board to become ready...");
                Thread.Sleep(3000);
            }
        });
        
        ConsoleHelper.PK("Press a key to send a test thingy");
        switchDevice.TurnOn();

        ConsoleHelper.PK("Press a key to send a test thingy");
        switchDevice.TurnOff();

        ConsoleHelper.PK("Press a key to disconnect");
        board.End();

        ConsoleHelper.PK("Press a key to end");
        board.End();

    }
}
