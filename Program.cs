using System.Threading;

namespace vaudio_openal;

public class Program
{
    public static void Main()
    {
        var scene = new Scene();

        while (true)
        {
            scene.Update();
            Thread.Sleep(16);
        }
    }
}
