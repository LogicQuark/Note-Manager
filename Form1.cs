using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NoteManager
{
    public partial class NoteManagerForm : Form
    {
        private bool autoSaveEnabled = false;
        private bool isSaved = false;
        private static string currentFilePath = null;
        private string tmpFilePath = null;

        private static Note defaultNote = new Note
        {
            Title = "",
            Created = DateTime.Now,
            FontSize = 12,
            Content = ""
        };

        private Note note = defaultNote;

        private struct Note
        {
            public string Title;
            public DateTime Created;
            public int FontSize;
            public string Content;
        }
        public NoteManagerForm()
        {
            InitializeComponent();

        }


        private void NoteManagerForm_Load(object sender, EventArgs e)
        {
            noteTextBox.Font = new Font(noteTextBox.Font.FontFamily, defaultNote.FontSize);
            showNote(note);
            this.Text = $"Note Manager - Untitled";
        }

        // Function converts a Note object into a byte array. ParseByteToNote() is the reverse function. 
        private byte[] ParseNoteToByte(Note n)
        {        
            
            byte[] titleBytes = Encoding.ASCII.GetBytes(n.Title);
            byte[] dateBytes = Encoding.ASCII.GetBytes(n.Created.ToString());
            byte[] fontBytes = Encoding.ASCII.GetBytes(n.FontSize.ToString());
            byte[] contentBytes = Encoding.ASCII.GetBytes(n.Content);

            // Array to hold the length of each section in uint16 format (exactly 2 bytes each)
            UInt16[] length =  new UInt16[] 
            {
                Convert.ToUInt16(titleBytes.Length),
                Convert.ToUInt16(dateBytes.Length),
                Convert.ToUInt16(fontBytes.Length),
                Convert.ToUInt16(contentBytes.Length)
            };            

            byte[] lengthBytes = new byte[2*length.Length];

            int totalLength = lengthBytes.Length + titleBytes.Length + dateBytes.Length + fontBytes.Length + contentBytes.Length;
            try
            {
                for (var i = 0; i< length.Length; i++)
                {                    
                    lengthBytes[i * 2] = (byte)(length[i] & 0xFF);
                                        
                    lengthBytes[(i * 2) + 1] = (byte)((length[i] >> 8) & 0xFF);
                }

                var noteBytes = new byte[totalLength];
                int offset = 0;
                Buffer.BlockCopy(lengthBytes, 0, noteBytes, offset, lengthBytes.Length);
                offset += lengthBytes.Length;
                Buffer.BlockCopy(titleBytes, 0, noteBytes, offset, titleBytes.Length);
                offset += titleBytes.Length;
                Buffer.BlockCopy(dateBytes, 0, noteBytes, offset, dateBytes.Length);
                offset += dateBytes.Length;
                Buffer.BlockCopy(fontBytes, 0, noteBytes, offset, fontBytes.Length);
                offset += fontBytes.Length;
                Buffer.BlockCopy(contentBytes, 0, noteBytes, offset, contentBytes.Length);

                return noteBytes;
            }
            catch { return null; }
            

        }

        // Function converts a byte array back into a Note object. Used when loading from binary format. ParseNoteToByte() is the reverse function.
        private Note ParseByteToNote(byte[] b)
        {
            Note n = new Note();
            var lengths = new UInt16[4] ;
            for (int i = 0; i < 7; i = i + 2)
            {
                lengths[i/2] = BitConverter.ToUInt16(b, i);
            }

                var offset = 8;
                var title = Encoding.ASCII.GetString(b, offset, lengths[0]);
                offset += lengths[0];
                var date = Encoding.ASCII.GetString(b, offset, lengths[1]);
                offset += lengths[1];
                var font = Encoding.ASCII.GetString(b, offset, lengths[2]);
                offset += lengths[2];
                var content = Encoding.ASCII.GetString(b, offset, lengths[3]);

                n.Title = title;
                n.Created = DateTime.Parse(date);
                n.FontSize = int.Parse(font);
                n.Content = content;
            
            

            return n;
        }

        private Note TryParseByteToNote(byte[] b)
        {
            try
            {
                note = ParseByteToNote(b);
            }
            catch
            {
                MessageBox.Show("Error parsing the note file. The file may be corrupted or in an unsupported format.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                note = defaultNote;
                currentFilePath = "Untitled";
            }
            return note;
        }

        private void showNote(Note n)
        {
            try
            {
                noteTextBox.Text = n.Content;
                titleTextBox.Text = n.Title;
                noteTextBox.Font = new Font(noteTextBox.Font.FontFamily, n.FontSize);
                statusUpdate();
            }
            catch { }

        }

        private void saveAsFile()
        {
            using (SaveFileDialog saveAsDialog = new SaveFileDialog())
            {
                saveAsDialog.Filter = "Binary and Text files (*.note; *.txt)|*.note;*.txt|Text files (*.txt)|*.txt|Binary files (*.note)|*.note";
                saveAsDialog.Title = "Save As";
                if (saveAsDialog.ShowDialog() == DialogResult.OK)
                {
                    currentFilePath = saveAsDialog.FileName;
                    var extension = Path.GetExtension(currentFilePath);
                        
                    switch (extension.ToLower())
                    {                       

                        case ".note":
                            using (FileStream fs = new FileStream(currentFilePath, FileMode.Create))
                            {
                                var buf = ParseNoteToByte(note);
                                if (buf != null)
                                {
                                    fs.Write(buf, 0, buf.Length);
                                }
                            }
                            break;

                        case ".txt":
                            File.WriteAllText(currentFilePath, noteTextBox.Text);
                            isSaved = true;
                            break;
                        default:
                            MessageBox.Show("Unsupported file format. Please choose a .txt or .note file.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            break;
                    }
                    isSaved = true;
                    
                    this.Text = $"Note Manager - {Path.GetFileName(currentFilePath)}";
                }
            }
            
        }
        
        private void openFile()
        {
            if (!isSaved & noteTextBox.Text != "")
            {
                if (noteTextBox.Text != "" & MessageBox.Show("Do you want to save changes?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    saveFile();
                }
            }
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Binary and Text files (*.note; *.txt)|*.note;*.txt|Text files (*.txt)|*.txt|Binary files (*.note)|*.note";
                openFileDialog.Title = "Open Note";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    currentFilePath = openFileDialog.FileName;
                    var extension = Path.GetExtension(currentFilePath);
                    switch (extension.ToLower())
                    {
                        

                        case ".note":
                            using (FileStream fs = new FileStream(currentFilePath, FileMode.Open))
                            {
                                var buf = new byte[fs.Length];
                                fs.Read(buf, 0, (int)fs.Length);
                                note = TryParseByteToNote(buf); 
                            }
                            break;

                        case ".txt":
                            note = defaultNote;
                            note.Content = File.ReadAllText(currentFilePath);
                            note.Created = DateTime.Now;

                            break;

                    }
                    note.Created = DateTime.Now;                    
                    showNote(note);
                    isSaved = true;
                }
                this.Text = $"Note Manager - {Path.GetFileName(currentFilePath)}";
            }
        }

        private void saveFile()
        {
            tmpFilePath = currentFilePath + ".tmp";
            if (!File.Exists(currentFilePath))
            {
                saveAsFile();
            }
            else{
                switch (Path.GetExtension(currentFilePath).ToLower())
                {
                    case ".txt":
                        File.WriteAllText(tmpFilePath, noteTextBox.Text);
                        break;

                    case ".note":
                        using (FileStream fs = new FileStream(tmpFilePath, FileMode.Create))
                        {
                            var buf = ParseNoteToByte(note);
                            fs.Write(buf, 0, buf.Length);
                        }
                        break;
                }
                File.Replace(tmpFilePath, currentFilePath, null);
                isSaved = true;

            }
        }

        private void newFile()
        {
            if (!isSaved & noteTextBox.Text != "")
            {
                if (MessageBox.Show("Do you want to save changes?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    saveFile();
                }
            }
            autoSaveEnabled = false;

            note = defaultNote;
            note.Created = DateTime.Now;

            noteTextBox.Font = new Font(noteTextBox.Font.FontFamily, note.FontSize);

            showNote(note);

            noteTextBox.Clear();
            currentFilePath = null;
            isSaved = false;

            this.Text = $"Note Manager - Untitled";


            if (autoSaveToolStripMenuItem.Checked ) { autoSaveEnabled = true; }
        }

        private void statusUpdate()
        {
            rowNumberStripStatusLabel.Text = $"R: {noteTextBox.Lines.Length}";
            symbolNumberStripStatusLabel.Text = $"S: {noteTextBox.Text.Length}";
            fontSizeStripStatusLabel.Text = $"Font size: {noteTextBox.Font.Size} pt";
            dateOfCreationStripStatusLabel.Text = $"Created: {note.Created.ToString()}";
        }

        private void searchTextInNote()
        {
            
            string searchText = searchTextBoxMenuItem.Text;
            if(searchText == null || searchText == "") { return; }

            int startPos = noteTextBox.Text.IndexOf(searchText, StringComparison.CurrentCultureIgnoreCase);
            if (startPos >= 0)
            {
                noteTextBox.Select(startPos, searchText.Length);
                noteTextBox.ScrollToCaret();
            }
            else
            {
                noteTextBox.Select(0, 0);
            }
        }

        private void autoSaveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (autoSaveToolStripMenuItem.Checked)
            {
                autoSaveEnabled = true;
            }
            else
            {
                autoSaveEnabled = false;
            }
        }


        private void noteTextBox_TextChanged(object sender, EventArgs e)
        {
            note.Content = noteTextBox.Text;
            statusUpdate();
            if (autoSaveEnabled) { saveFile(); }
            else { isSaved = false; }
        }

        private void searchTextBoxMenuItem_Enter(object sender, EventArgs e)
        {
            searchTextBoxMenuItem.Text = "";
            searchTextBoxMenuItem.ForeColor = Color.Black;
        }

        private void searchTextBoxMenuItem_Leave(object sender, EventArgs e)
        {
            if(searchTextBoxMenuItem.Text == "")
            {
                searchTextBoxMenuItem.ForeColor = Color.Gray;
                searchTextBoxMenuItem.Text = "Search in note";
            }

        }

        private void searchTextBoxMenuItem_KeyPress(object sender, KeyPressEventArgs e)
        {
            //searchTextInNote();
        }

        private void fontToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (FontDialog fontSelectDialog = new FontDialog()){

                if (fontSelectDialog.ShowDialog() == DialogResult.OK)
                {
                    note.FontSize = (int)fontSelectDialog.Font.Size;
                    noteTextBox.Font = fontSelectDialog.Font;
                } 
                showNote(note);
            }
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            saveFile();
        }

        private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            saveAsFile();
        }

        private void newToolStripMenuItem_Click(object sender, EventArgs e)
        {
            newFile();        
            
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            openFile();
            
        }

        private void reToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void newToolStripButton_Click(object sender, EventArgs e)
        {
            newFile();
        }

        private void openToolStripButton_Click(object sender, EventArgs e)
        {
            openFile();
        }

        private void saveToolStripButton_Click(object sender, EventArgs e)
        {
            saveFile();
        }

        private void saveAsToolStripButton_Click(object sender, EventArgs e)
        {
            saveAsFile();
        }

        private void titleTextBox_TextChanged(object sender, EventArgs e)
        {
            note.Title = titleTextBox.Text;
            if (autoSaveEnabled) { saveFile(); }
            else { isSaved = false; }
        }

        private void searchTextBoxMenuItem_TextChanged(object sender, EventArgs e)
        {
            searchTextInNote();
        }
    }
}
