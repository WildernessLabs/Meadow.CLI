namespace Meadow.CLI.Commands.Current.Project
{
    internal class MeadowTemplate
    {
        public string Name;
        public string ShortName;

        public MeadowTemplate(string longName, string shortName)
        {
            this.Name = longName;
            this.ShortName = shortName;
        }
    }
}