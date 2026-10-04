using FolderDupesDLL;

namespace FolderDupesUI
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        /// 
        static Form1 MainForm;

        [STAThread]
        static void Main()
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            Application.Run(MainForm = new Form1());
        }

        static bool resultsReady = false;

		public enum Status
		{
            IDLE,READING,COMPARING,UNIQING
		}

        public static Status status = Status.IDLE;
        static Dupes dupes;

        public static void start()
		{
            List<String> includeFolders = new List<string>();
            //List<String> includeFolders = new List<string>(new string[] { @"m:\foto" });
            List<String> excludeFolders = new List<string>(new string[] { @"\\LRplugins", @"\.lrdata$", @"\\node_modules", @"\\\.git", @"\.scriv", @"\.picasa\.ini", @"Thumbs\.db", @"\.svn" });

            includeFolders.Add( @"C:\FOTO" );
            dupes = new Dupes(includeFolders:includeFolders.ToArray(),excludePatterns: excludeFolders.ToArray());

            Thread t = new Thread(() => {

                status = Status.READING;
                MainForm.Invoke(MainForm.displayDupeStatus);
                dupes.Enumerate();
                
                status = Status.COMPARING;
                MainForm.Invoke(MainForm.displayDupeStatus);
                dupes.RunComparison();
                
                status = Status.UNIQING;
                MainForm.Invoke(MainForm.displayDupeStatus);
                dupes.CalculateFolderUniquity();

                status = Status.IDLE;
                MainForm.Invoke(MainForm.displayDupeStatus);
                MainForm.Invoke(MainForm.displayDupes);
            } );
            t.Name = "dupereader";
            t.Start();
        }
    }
}