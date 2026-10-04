using FolderDupesDLL;

namespace FolderDupesUI
{
    public partial class Form1 : Form
    {
        Dupes dupes;

        public Form1()
        {
            InitializeComponent();
        }

		private void butStart_Click(object sender, EventArgs e)
		{
			Program.start();
		}

        public void focusOnFolder(string f)
		{
            var dir = new DirectoryInfo(f);
            var files = dir.GetFiles();
            listView2.Items.Clear();
            foreach (var file in files)
			{
                var item = new ListViewItem(file.Name);
                listView2.Items.Add(item);
                var bu = dupes.GetDupes(file.FullName);
                if (bu != null)
                    item.SubItems.Add(bu.Count.ToString());
			}
		}

		public void displayDupes()
		{
            listView2.Items.Add(dupes.Files.Count + " files in " + dupes.Folders.Count + " folders found.");

            tableLayoutPanel1.Controls.Clear();

            string[] folders = dupes.FolderUniquities.Keys.ToArray();
            folders = folders.Where(t => !dupes.SkipUniquities.Contains(t)).ToArray();
            var ordered = folders.OrderBy(f => dupes.FolderUniquities[f].duplicity);

            string[] listDirs(Dictionary<string, int> dir, Dupes.folderUniquity me_uniq)
            {
                List<string> result = new List<string>();
                foreach (string dirName in dir.Keys)
                {
                    var dupe_uniq = dupes.FolderUniquities[dirName];
                    result.Add(String.Format("  {0} : {1}{2}", dirName, dir[dirName], (me_uniq.unique == dupe_uniq.unique && me_uniq.dupes.Count() == dupe_uniq.dupes.Count()) ? " = EQUAL" : ""));
                }
                return result.ToArray();
            }

            int MinDupes = 5;
            var skips = new Dictionary<string, bool>();
            foreach (var o in ordered)
            {
                var fuq = dupes.FolderUniquities[o];
                if (fuq.dupes.Length < MinDupes) continue;
                if (skips.ContainsKey(o)) continue;
                if (fuq.totallyDuped && dupes.FolderUniquities.TryGetValue(o.Remove(o.LastIndexOf('\\')), out var fuq2) && fuq2.totallyDuped) continue; // parent is fully duped, too

                Label l = new Label();
                var dirs = listDirs(fuq.dupeDirs, fuq);
                l.Text = o + " - " + (fuq.duplicity * 100).ToString("0") + "% duped:\n" + String.Join("\n", dirs) + "\n";
                l.AutoSize = true;
                l.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                l.Click += (e,i) => focusOnFolder(o);
                l.MouseEnter += (e, i) => l.ForeColor = SystemColors.HotTrack;
                l.MouseLeave += (e, i) => l.ForeColor = SystemColors.ControlText;
                l.Cursor = Cursors.Hand;

                tableLayoutPanel1.Controls.Add(l);
                tableLayoutPanel1.RowStyles[tableLayoutPanel1.RowCount - 1].SizeType = SizeType.AutoSize;
                
            }


		}

		public void displayDupeStatus()
		{
			label1.Text = Program.status.ToString() + "\nFolders:" + dupes.Folders.Count.ToString() + "\nFiles: " + dupes.Files.Count.ToString();
		}

		private void timer1_Tick(object sender, EventArgs e)
		{
			displayDupeStatus();
		}
	}
}