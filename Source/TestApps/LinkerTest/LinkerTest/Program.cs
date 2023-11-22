namespace LinkerTest
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello, World!");

            var linker = new MeadowLinker();

            string fileToLink = @"H:\WL\Meadow.ProjectLab\Source\ProjectLab_Demo\bin\Debug\netstandard2.1\App.dll";

            linker.TrimApplication(new FileInfo(fileToLink), true);
        }
    }
}
