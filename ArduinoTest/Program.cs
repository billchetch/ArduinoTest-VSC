using System.IO.Ports;
using Chetch.Messaging;
using Chetch.Arduino;
using Chetch.Arduino.Devices;
using System.Threading.Tasks;

namespace ArduinoTest;

class Program
{
    static async void foo(){
        Task t = Task.Run(()=>{

            throw new Exception("Fuck this");
        });
        try{
            await t;
        } catch (AggregateException e){
            Console.WriteLine("Some bullshit");
        }
    }

    static void Main(string[] args)
    {
        //ConsoleHelper.PK("Press a key to start");
       
        //foo();

        //ArduinoBoard board = new ArduinoBoard(0x0043, 9600, Frame.FrameSchema.SMALL_NO_CHECKSUM);
        ArduinoBoard board = new ArduinoBoard("first", 0x7523, 9600); //, Frame.FrameSchema.SMALL_NO_CHECKSUM);
        board.Ready += (sender, ready) => {
            Console.WriteLine("Board is ready: {0}", ready);
            Console.WriteLine("Free memory: {0}", board.FreeMemory);
        };

        board.MessageReceived += (sender, message) => {
            Console.WriteLine("Message received!");
        };

        var device = new Ticker("testDevice01");
        device.Updated += (sender, properties) => {
            Console.WriteLine(device.Count);
        };
        
        board.AddDevice(device);
        try
        {
            board.Begin();
        }
        catch(Exception e)
        {
            Console.WriteLine(e.Message);
        }

        ConsoleHelper.PK("Press a key to disconnect");
        board.End();

        ConsoleHelper.PK("Press a key to end");
        
    }
}
