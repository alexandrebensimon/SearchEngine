﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using SearchEngineProject.Properties;

namespace SearchEngineProject
{
    public partial class MainWindow : Form
    {
        private DiskPositionalIndex _index;
        private List<string> _finalResults;
        private int _numberOfResultsByPage = 10;
        private int _currentPage = 1;
        private int _numberOfPages;
        private FormWindowState _formerWindowsState = FormWindowState.Normal;
        private string _directoryPath;

        public MainWindow()
        {
            InitializeComponent();
            labelIndexing.Hide();
        }

        #region Menu

        #region Index menu item

        private void quitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Dispose();
            Close();
        }

        private void statisticsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var statistics = new StringBuilder();
            statistics.Append("Number of terms in the index: ");
            statistics.AppendLine(_index.IndexSize.ToString() + " terms\n");

            statistics.Append("Average number of documents in the postings list: ");
            statistics.AppendLine(_index.AvgNumberDocsInPostingsList.ToString() + " documents\n");

            statistics.AppendLine("Proportion of documents that contain each of the 10 most frequent terms:");
            foreach (var pair in _index.ProportionDocContaining10MostFrequent)
            {
                statistics.Append(pair.Key + ": " + Math.Round(pair.Value, 2) * 100 + "%; ");
            }
            statistics.AppendLine("\n");

            statistics.Append("Approximate memory requirement of the index: ");
            statistics.Append(prettyBytes(_index.IndexSizeInMemory));

            MessageBox.Show(statistics.ToString(), Resources.StatMessageBoxTitle);
        }

        private void indexADirectoryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var fbd = new FolderBrowserDialog
            {
                ShowNewFolderButton = false,
                Description = "Choose the directory you want to index"
            };
            fbd.ShowDialog();
            _directoryPath = fbd.SelectedPath;

            if (string.IsNullOrEmpty(_directoryPath)) return;

            var filenames = Directory.GetFiles(_directoryPath, "*.bin")
                                     .Select(Path.GetFileNameWithoutExtension)
                                     .ToArray();

            DialogResult result = DialogResult.No;
            if (filenames.Contains("kGramIndex") && filenames.Contains("kGramVocab") && filenames.Contains("kGram") && filenames.Contains("vocab") && filenames.Contains("vocabTable") && filenames.Contains("postings") && filenames.Contains("statistics") && filenames.Contains("mostFreqWord") && filenames.Contains("docWeights") && filenames.Contains("matrix") && filenames.Contains("vocabMatrix") && filenames.Contains("vocabTableMatrix"))
                result = MessageBox.Show("This directory is already indexed, let's skip the long indexation! :)", "Directory already indexed", MessageBoxButtons.YesNo);

            if (result == DialogResult.No)
            {
                if (_index != null)
                    _index.Dispose();
                
                labelIndexing.Show();
                panelArticle.Hide();
                panelResults.Hide();
                panelSearch.Hide();
                labelIndexing.BringToFront();
                progressBar.BringToFront();
                Update();
                var writer = new IndexWriter(_directoryPath);
                writer.BuildIndex(this);

                //Write the KGram Index to the disk
                KGramIndex.ToDisk(_directoryPath);
            }

            //Load the Disk positional index into memory
            _index = new DiskPositionalIndex(_directoryPath);

            //Load the KGram index in memory
            KGramIndex.ToMemory(_directoryPath);

            //Load the matrix to memory
            QueryReformulation.ToMemory(_directoryPath);

            toolStripMenuItemStatistics.Enabled = true;
            labelIndexing.Hide();
            textBoxSearch.Enabled = true;
            textBoxSearch.Select();
            textBoxSearch.Text = "Indexing done ^^";
            textBoxSearch.SelectionStart = 0;
            textBoxSearch.SelectionLength = textBoxSearch.Text.Length;

            checkBoxBool.Enabled = true;
            checkBoxRank.Enabled = true;
        }

        #endregion

        #region Index menu help

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Class: CECS 529\n" +
                "Search Engine Technology Project\n" +
                "Authors: Alexandre Bensimon and Vincent Gagneux\n\n" +
                "Milestone 1 options: Wildcard queries, Syntax checking, GUI, Index statistics\n" +
                "Milestone 2 options: Spelling correction, K-gram index on disk, NOT queries\n" +
                "Milestone 3 options: Query expansion", "About");
        }

        #endregion

        #endregion

        #region Search textBox

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            if (checkBoxBool.Checked)
                DisplayBooleanSearchResults();
            else if (checkBoxRank.Checked)
                DisplayRankSearchResults();
        }

        private void searchTextBox_Click(object sender, EventArgs e)
        {
            if (textBoxSearch.Text == "Indexing done ^^")
                textBoxSearch.Clear();
        }

        #endregion

        #region Retrieval modes

        private void DisplayRankSearchResults()
        {
            tableLayoutPanelResults.Controls.Clear();
            SimpleEngine.FoundTerms.Clear();
            labelNumberResults.Text = string.Empty;
            labelCorrectedWord.Hide();
            panelQueryPropositions.Hide();

            var query = textBoxSearch.Text.ToLower();

            var results = SimpleEngine.ProcessRankQuery(query, _index, _directoryPath);

            IList<KeyValuePair<int, double>> keyValuePairs = new List<KeyValuePair<int, double>>();
            if (results != null) keyValuePairs = results as IList<KeyValuePair<int, double>> ?? results.ToList();

            if (!keyValuePairs.Any() || results == null)
            {
                tableLayoutPanelResults.Controls.Add(new Label
                {
                    Text = "No results",
                    AutoSize = true,
                    Font = new Font("Segoe Print", (float)14.25)
                });
                labelNumberResults.Hide();
                UpdatePageNumber();
            }
                
            else
            {
                var temp = SimpleEngine.ProcessQuery(query, _index);
                int numberOfResults = temp.Count;
                if (numberOfResults > 10) numberOfResults = 10;

                labelNumberResults.Show();
                labelNumberResults.Text = numberOfResults + " results";

                _finalResults = new List<string>();
                for (int i = 0; i < numberOfResults; i++)
                {
                    _finalResults.Add(_index.FileNames[keyValuePairs.ElementAt(i).Key]);
                    _finalResults.Add(keyValuePairs.ElementAt(i).Value.ToString());
                }

                UpdateDisplayResults(_currentPage);
                buttonPrevious.Visible = true;
                buttonNext.Visible = true;
            }
        }

        private void DisplayBooleanSearchResults()
        {
            tableLayoutPanelResults.Controls.Clear();
            SimpleEngine.FoundTerms.Clear();
            labelNumberResults.Text = string.Empty;
            var query = textBoxSearch.Text.ToLower();
            labelCorrectedWord.Hide();
            panelQueryPropositions.Hide();

            var resultsDocIds = SimpleEngine.ProcessQuery(query, _index);

            if (resultsDocIds == null)
            {
                tableLayoutPanelResults.Controls.Add(new Label
                {
                    Text = "Wrong syntax",
                    AutoSize = true,
                    Font = new Font("Segoe Print", (float)14.25)
                });
                labelNumberResults.Hide();
                UpdatePageNumber();
            }
                
            else if (resultsDocIds.Count == 0)
            {
                tableLayoutPanelResults.Controls.Add(new Label
                {
                    Text = "No results",
                    AutoSize = true,
                    Font = new Font("Segoe Print", (float)14.25)
                });
                labelNumberResults.Hide();
                UpdatePageNumber();
            }
            else
            {
                // Display the number of returned documents.
                labelNumberResults.Show();
                labelNumberResults.Text = resultsDocIds.Count + " results";

                // Build the results.
                _finalResults = new List<string>();
                foreach (int docId in resultsDocIds)
                {
                    _finalResults.Add(_index.FileNames[docId]);
                }

                UpdateDisplayResults(_currentPage);
                buttonPrevious.Visible = true;
                buttonNext.Visible = true;
            }

            //Display potential correction of search terms if needed
            if (SimpleEngine.PotentialMisspelledWords.Any())
            {
                string correctedQuery = textBoxSearch.Text;
                bool correctionFound = false;

                foreach (var potentialMisspelledWord in SimpleEngine.PotentialMisspelledWords)
                {
                    var correctedWords = KGramIndex.GetCorrectedWord(potentialMisspelledWord);

                    if (correctedWords.Count > 1)
                    {
                        int maxDocumentFreq = 0;
                        string correctedWord = null;
                        foreach (var word in correctedWords)
                        {
                            var termDocFreq = _index.GetPostings(PorterStemmer.ProcessToken(word), false).Count();
                            if (termDocFreq > maxDocumentFreq)
                            {
                                maxDocumentFreq = termDocFreq;
                                correctedWord = word;
                            }
                        }
                        correctedQuery = correctedQuery.Replace(potentialMisspelledWord, correctedWord);
                        correctionFound = true;
                    }
                    else if (correctedWords.Count == 1)
                    {
                        correctedQuery = correctedQuery.Replace(potentialMisspelledWord, correctedWords.First());
                        correctionFound = true;
                    }
                }

                if (!correctionFound) return;

                labelCorrectedWord.Show();
                labelCorrectedWord.Text = "Did you mean: " + correctedQuery + "?";
            }
            
            else
            {
                // Display query proposition
                var newText = textBoxSearch.Text.Trim();
                if (newText.Split(' ').Count() == 1 && newText != String.Empty)
                {
                    var token = newText;
                    var wordsList = QueryReformulation.GetExtendedQueries(PorterStemmer.ProcessToken(token));
                    if (wordsList != null)
                    {
                        panelQueryPropositions.Controls.Clear();
                        panelQueryPropositions.Show();
                        panelQueryPropositions.Controls.Add(new Label { Text = "Try with:", AutoSize = true, Font = new Font("Segoe Print", 11), ForeColor = Color.Firebrick });

                        for (int i = 0; i < 3; i++)
                        {
                            var propositionLabel = new Label
                            {
                                Text = token + ' ' + wordsList.ElementAt(i),
                                AutoSize = true,
                                Font = new Font("Segoe Print", 10),
                                ForeColor = Color.Firebrick
                            };
                            propositionLabel.Click += PropositionLabel_Click;
                            propositionLabel.MouseEnter += PropositionLabel_MouseEnter;
                            propositionLabel.MouseLeave += PropositionLabel_MouseLeave;
                            panelQueryPropositions.Controls.Add(propositionLabel);
                        }
                    }
                }
            }
            
        }

        #endregion

        #region RetrievalMods Checkboxes

        private void checkBox1_Click(object sender, EventArgs e)
        {
            checkBoxBool.CheckState = CheckState.Checked;
            checkBoxBool.ForeColor = Color.Black;
            checkBoxBool.FlatAppearance.MouseOverBackColor = Color.Gold;
            checkBoxBool.FlatAppearance.MouseDownBackColor = Color.Gold;

            checkBoxRank.CheckState = CheckState.Unchecked;
            checkBoxRank.ForeColor = Color.Gold;
            checkBoxRank.FlatAppearance.MouseOverBackColor = Color.FromArgb(64, 64, 64);
            checkBoxRank.FlatAppearance.MouseDownBackColor = Color.FromArgb(64, 64, 64);

            DisplayBooleanSearchResults();
        }

        private void checkBox2_Click(object sender, EventArgs e)
        {
            checkBoxRank.CheckState = CheckState.Checked;
            checkBoxRank.ForeColor = Color.Black;
            checkBoxRank.FlatAppearance.MouseOverBackColor = Color.Gold;
            checkBoxRank.FlatAppearance.MouseDownBackColor = Color.Gold;

            checkBoxBool.CheckState = CheckState.Unchecked;
            checkBoxBool.ForeColor = Color.Gold;
            checkBoxBool.FlatAppearance.MouseOverBackColor = Color.FromArgb(64, 64, 64);
            checkBoxBool.FlatAppearance.MouseDownBackColor = Color.FromArgb(64, 64, 64);

            DisplayRankSearchResults();
        }

        #endregion

        #region Results display

        #region Filename labels

        private void UpdateDisplayResults(int pageToDisplay)
        {
            tableLayoutPanelResults.Controls.Clear();

            UpdatePageNumber();

            if (_currentPage != 0)
            {
                for (int i = (pageToDisplay * _numberOfResultsByPage) - _numberOfResultsByPage; i < pageToDisplay * _numberOfResultsByPage; i++)
                {
                    if (_finalResults.Count <= i) break;
                    AddNewLabel(_finalResults.ElementAt(i));
                }
            }
        }

        private void PropositionLabel_Click(object sender, EventArgs e)
        {
            var label = sender as Label;

            if (label != null)
            {
                textBoxSearch.Text = label.Text;
                DisplayBooleanSearchResults();
            }
        }

        private void PropositionLabel_MouseEnter(object sender, EventArgs e)
        {
            var label = sender as Label;

            if (label != null)
            {
                label.Cursor = Cursors.Hand;
                label.Font = new Font(label.Font.Name, label.Font.SizeInPoints, FontStyle.Underline);
            }
        }

        private void PropositionLabel_MouseLeave(object sender, EventArgs e)
        {
            var label = sender as Label;

            if (label != null)
            {
                label.Cursor = Cursors.Default;
                label.Font = new Font(label.Font.Name, label.Font.SizeInPoints, FontStyle.Regular);
            }
        }

        private void FileNameLabel_MouseEnter(object sender, EventArgs e)
        {
            var label = sender as Label;

            if (label != null)
            {
                label.Cursor = Cursors.Hand;
                label.Font = new Font(label.Font.Name, label.Font.SizeInPoints, FontStyle.Underline);
            }
        }

        private void FileNameLabel_MouseLeave(object sender, EventArgs e)
        {
            var label = sender as Label;

            if (label != null)
            {
                label.Cursor = Cursors.Default;
                label.Font = new Font(label.Font.Name, label.Font.SizeInPoints, FontStyle.Regular);
            }
        }

        private void AddNewLabel(string text)
        {
            var fileNameLabel = new Label
            {
                Text = text,
                AutoSize = true,
                Font = new Font("Segoe Print", (float)14.25)
            };
            fileNameLabel.Click += FileNameLabel_Click;
            fileNameLabel.MouseEnter += FileNameLabel_MouseEnter;
            fileNameLabel.MouseLeave += FileNameLabel_MouseLeave;
            tableLayoutPanelResults.Controls.Add(fileNameLabel);
        }

        private void FileNameLabel_Click(object sender, EventArgs e)
        {
            var label = sender as Label;

            if (label != null)
            {
                foreach (Label tempLabel in tableLayoutPanelResults.Controls)
                {
                    tempLabel.ForeColor = Color.Black;
                }
                label.ForeColor = Color.Gold;
                try
                {
                    textBoxArticle.Text = File.ReadAllText(_directoryPath + "/" + label.Text);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }

                // Highlight the search terms.
                HighlightText();
            }

        }

        #endregion

        #region Error correction

        private void correctedWordLabel_MouseEnter(object sender, EventArgs e)
        {
            labelCorrectedWord.Font = new Font(labelCorrectedWord.Font, FontStyle.Underline);
        }

        private void correctedWordLabel_MouseLeave(object sender, EventArgs e)
        {
            labelCorrectedWord.Font = new Font(labelCorrectedWord.Font, FontStyle.Regular);
        }

        private void correctedWordLabel_MouseClick(object sender, MouseEventArgs e)
        {
            textBoxSearch.Text = labelCorrectedWord.Text.Replace("Did you mean: ", "").Replace("?", "");
        }

        #endregion

        #region Page management

        private void nextButton_Click(object sender, EventArgs e)
        {
            _currentPage++;
            UpdateDisplayResults(_currentPage);
        }

        private void previousButton_Click(object sender, EventArgs e)
        {
            _currentPage--;
            UpdateDisplayResults(_currentPage);
        }

        private void UpdateNumberOfResultsDisplayed()
        {
            int tmp = Size.Height;
            _numberOfResultsByPage = 10;
            tableLayoutPanelResults.RowCount = 0;

            while (tmp > MinimumSize.Height + 32)
            {
                _numberOfResultsByPage += 2;
                tmp -= 32;
            }

            UpdatePageNumber();
            UpdateDisplayResults(_currentPage);
        }

        private void nextButton_EnabledChanged(object sender, EventArgs e)
        {
            if (buttonNext.Enabled)
            {
                buttonNext.BackColor = Color.Gold;
                buttonNext.ForeColor = Color.Black;
            }
            else
            {
                buttonNext.BackColor = Color.FromArgb(64, 64, 64);
                buttonNext.ForeColor = Color.Gold;
            }
        }

        private void previousButton_EnabledChanged(object sender, EventArgs e)
        {
            if (buttonPrevious.Enabled)
            {
                buttonPrevious.BackColor = Color.Gold;
                buttonPrevious.ForeColor = Color.Black;
            }
            else
            {
                buttonPrevious.BackColor = Color.FromArgb(64, 64, 64);
                buttonPrevious.ForeColor = Color.Gold;
            }
        }

        private void UpdatePageNumber()
        {
            int numberOfResults = 0;
            if (labelNumberResults.Visible)
                numberOfResults = int.Parse(labelNumberResults.Text.Remove(labelNumberResults.Text.Length - 8));
            if (checkBoxRank.Checked) numberOfResults = numberOfResults * 2;
            _numberOfPages = (int)Math.Ceiling((double)numberOfResults / _numberOfResultsByPage);

            if (_numberOfPages == 0) _numberOfPages = 1;
            if (_currentPage == 0) _currentPage = 1;

            while (_currentPage > _numberOfPages) _currentPage--;

            labelPage.Text = _currentPage + "/" + _numberOfPages;

            buttonPrevious.Enabled = _currentPage != 1;

            buttonNext.Enabled = _currentPage != _numberOfPages;
        }

        #endregion

        #endregion

        #region Article textBox

        public void HighlightText()
        {

            int sStart = textBoxArticle.SelectionStart, startIndex = 0;

            foreach (var articleWord in textBoxArticle.Text.Split(new char[] { ' ', '-' }))
            {
                var cleanWord = Regex.Replace(articleWord, @"[^-\w\s]*", "").ToLower();
                if (SimpleEngine.FoundTerms.Contains(PorterStemmer.ProcessToken(cleanWord)))
                {
                    var index = textBoxArticle.Text.IndexOf(articleWord, startIndex, StringComparison.Ordinal);
                    textBoxArticle.Select(index, articleWord.Length);
                    textBoxArticle.SelectionColor = Color.Gold;
                    textBoxArticle.SelectionBackColor = Color.Black;
                }
                startIndex += articleWord.Length + 1;
            }

            textBoxArticle.SelectionStart = sStart;
            textBoxArticle.SelectionLength = 0;
        }

        #endregion

        #region Form

        private void MainWindow_ResizeEnd(object sender, EventArgs e)
        {
            if (buttonPrevious.Visible)
                UpdateNumberOfResultsDisplayed();
        }

        private void MainWindow_SizeChanged(object sender, EventArgs e)
        {
            if (WindowState != _formerWindowsState && buttonPrevious.Visible)
            {
                _formerWindowsState = WindowState;
                UpdateNumberOfResultsDisplayed();
            }
        }

        #endregion

        #region ProgressBar

        public void InitiateprogressBar(int numberOfDocuments)
        {
            // Display the ProgressBar control.
            progressBar.Visible = true;
            // Set Minimum to 1 to represent the first file being copied.
            progressBar.Minimum = 1;
            // Set Maximum to the total number of files to copy.
            progressBar.Maximum = numberOfDocuments;
            // Set the initial value of the ProgressBar.
            progressBar.Value = 1;
            // Set the Step property to a value of 1 to represent each file being copied.
            progressBar.Step = 1;
        }

        public void IncrementProgressBar()
        {
            progressBar.PerformStep();
            labelIndexing.Text = "We are indexing the directory for you :)\n" + progressBar.Value * 100 / progressBar.Maximum + "%";
            labelIndexing.Update();
        }

        public void HideProgressBar()
        {
            labelIndexing.Hide();
            progressBar.Hide();
            Update();
            panelArticle.Show();
            panelResults.Show();
            panelSearch.Show();
            Update();
        }

        #endregion

        #region Others

        private string prettyBytes(long numberOfBytes)
        {
            var counter = 0;
            var unit = new[] { "B", "KB", "MB", "GB" };
            while (numberOfBytes > 1024)
            {
                numberOfBytes /= 1024;
                counter++;
            }
            return numberOfBytes + unit[counter];
        }

        #endregion
    }
}
