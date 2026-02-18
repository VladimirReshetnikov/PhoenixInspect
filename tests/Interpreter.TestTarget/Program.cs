using System.Runtime.CompilerServices;

public static class Program
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void RetOnly()
    {
    }

    public static void Main(string[] args)
    {
        RetOnly();

        Console.WriteLine("READY");
        Console.Out.Flush();

        Thread.Sleep(Timeout.Infinite);
    }
}
