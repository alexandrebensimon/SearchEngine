﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SearchEngineProject
{
    public class DiskPositionalIndex : IDisposable
    {
        private readonly FileStream _mVocabList;
        private readonly FileStream _mPostings;
        private readonly long[] _mVocabTable;

        public int IndexSize { get; private set; }
        public int AvgNumberDocsInPostingsList { get; private set; }
        public Dictionary<string, double> ProportionDocContaining10MostFrequent { get; private set; }
        public long IndexSizeInMemory { get; private set; }
        public List<string> FileNames { get; }

        // CONSTRUCTOR
        public DiskPositionalIndex(string path)
        {
            // Open the vocabulary table and read it into memory. We will end up with an array of T pairs
            // of longs, where the first value is a position in the vocabularyTable file, and the second is
            // a position in the postings file.

            _mVocabList = new FileStream(Path.Combine(path, "vocab.bin"), FileMode.Open, FileAccess.Read);
            _mPostings = new FileStream(Path.Combine(path, "postings.bin"), FileMode.Open, FileAccess.Read);

            _mVocabTable = ReadVocabTable(path);
            FileNames = ReadFileNames(path);

            // Read index statistics.
            ReadStats(path);
        }

        // PUBLIC FUNCTIONS 
        public int[][] GetPostings(string term, bool positionsRequested)
        {
            long postingsPosition = BinarySearchVocabulary(term);
            if (postingsPosition >= 0)
                return ReadPostingsFromFile(_mPostings, postingsPosition, positionsRequested);
            return null;
        }

        public void Dispose()
        {
            if (_mVocabList != null)
                _mVocabList.Close();
            if (_mPostings != null)
                _mPostings.Close();
        }

        // PRIVATE FUNCTIONS
        private static int[][] ReadPostingsFromFile(FileStream postings, long postingsPosition, 
            bool positionsRequested)
        {
            // Seek the specified position in the file.
            postings.Seek(postingsPosition, SeekOrigin.Begin);

            // Read 4 bytes from the file into a buffer, for the document frequency.
            byte[] buffer = new byte[4];
            postings.Read(buffer, 0, buffer.Length);

            // The next two lines deal with Endianness issues and should be used every time a read is done.
            if (BitConverter.IsLittleEndian)
                Array.Reverse(buffer);

            // Convert the byte array to an int.
            int documentFrequency = BitConverter.ToInt32(buffer, 0);

            // Initialize the array of document IDs to return.
            int[][] postingsArray = new int[documentFrequency][];

            int previousDocId = 0;
            for (int i = 0; i < documentFrequency; i++)
            {
                //Read the document ID
                buffer = new byte[4];
                postings.Read(buffer, 0, buffer.Length);

                if (BitConverter.IsLittleEndian)
                    Array.Reverse(buffer);

                int docGap = BitConverter.ToInt32(buffer, 0);
                previousDocId += docGap;

                //Read the term frequency in the document
                buffer = new byte[4];
                postings.Read(buffer, 0, buffer.Length);

                if (BitConverter.IsLittleEndian)
                    Array.Reverse(buffer);

                int termFrequency = BitConverter.ToInt32(buffer, 0);

                if (positionsRequested)
                {
                    postingsArray[i] = new int[termFrequency + 1];
                    postingsArray[i][0] = previousDocId;

                    int previousPos = 0;
                    for (int j = 0; j < termFrequency; j++)
                    {
                        //Read a position
                        buffer = new byte[4];
                        postings.Read(buffer, 0, buffer.Length);

                        if (BitConverter.IsLittleEndian)
                            Array.Reverse(buffer);

                        int posGap = BitConverter.ToInt32(buffer, 0);
                        previousPos += posGap;
                        postingsArray[i][j + 1] = previousPos;
                    }
                }
                else
                {
                    postingsArray[i] = new int[1];
                    postingsArray[i][0] = previousDocId;

                    // TODO: Améliorer cette partie, on peut seek plus loin peut etre.
                    buffer = new byte[4 * termFrequency];
                    postings.Read(buffer, 0, buffer.Length);

                    if (BitConverter.IsLittleEndian)
                        Array.Reverse(buffer);
                }
            }

            return postingsArray;
        }

        private long BinarySearchVocabulary(string term)
        {
            // Do a binary search over the vocabulary, using the vocabTable and the file vocabList.
            int i = 0, j = _mVocabTable.Length / 2 - 1;
            while (i <= j)
            {
                int m = (i + j) / 2;
                long vListPosition = _mVocabTable[m * 2];
                int termLength;
                if (m == _mVocabTable.Length / 2 - 1)
                {
                    termLength = (int)(_mVocabList.Length - _mVocabTable[m * 2]);
                }
                else
                {
                    termLength = (int)(_mVocabTable[(m + 1) * 2] - vListPosition);
                }
                _mVocabList.Seek(vListPosition, SeekOrigin.Begin);

                byte[] buffer = new byte[termLength];
                _mVocabList.Read(buffer, 0, termLength);
                string fileTerm = Encoding.ASCII.GetString(buffer);

                int compareValue = String.Compare(term, fileTerm, StringComparison.Ordinal);
                if (compareValue == 0)
                {
                    // found it!
                    return _mVocabTable[m * 2 + 1];
                }
                if (compareValue < 0)
                {
                    j = m - 1;
                }
                else
                {
                    i = m + 1;
                }
            }
            return -1;
        }

        private static List<string> ReadFileNames(string indexName)
        {
            var names = new List<string>();
            foreach (string fileName in Directory.EnumerateFiles(
                Path.Combine(Environment.CurrentDirectory, indexName)))
            {
                if (fileName.EndsWith(".txt"))
                {
                    names.Add(Path.GetFileName(fileName));
                }
            }
            return names;
        }

        private static long[] ReadVocabTable(string indexName)
        {
            FileStream tableFile = new FileStream(
                Path.Combine(indexName, "vocabTable.bin"),
                FileMode.Open, FileAccess.Read);

            byte[] byteBuffer = new byte[4];
            tableFile.Read(byteBuffer, 0, byteBuffer.Length);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(byteBuffer);

            int tableIndex = 0;
            var vocabTable = new long[BitConverter.ToInt32(byteBuffer, 0) * 2];
            byteBuffer = new byte[8];

            while (tableFile.Read(byteBuffer, 0, byteBuffer.Length) > 0)
            { // While we keep reading 4 bytes.
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(byteBuffer);
                vocabTable[tableIndex] = BitConverter.ToInt64(byteBuffer, 0);
                tableIndex++;
            }
            tableFile.Close();
            return vocabTable;
        }

        private void ReadStats(string path)
        {
            var statFile = new FileStream(Path.Combine(path, "statistics.bin"), FileMode.Open, FileAccess.Read);
            var mostFreqFile = new FileStream(Path.Combine(path, "mostFreqWord.bin"), FileMode.Open, FileAccess.Read);
            ProportionDocContaining10MostFrequent = new Dictionary<string, double>();

            // Read the index size.
            var buffer = new byte[4];
            statFile.Read(buffer, 0, buffer.Length);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(buffer);
            IndexSize = BitConverter.ToInt32(buffer, 0);

            // Average number of docs in postings list.
            buffer = new byte[4];
            statFile.Read(buffer, 0, buffer.Length);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(buffer);
            AvgNumberDocsInPostingsList = BitConverter.ToInt32(buffer, 0);

            for (int i = 0; i < 10; i++)
            {
                // Read the length of the word.
                buffer = new byte[4];
                statFile.Read(buffer, 0, buffer.Length);
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(buffer);
                int wordLength = BitConverter.ToInt32(buffer, 0);

                // Read the word.
                buffer = new byte[wordLength];
                mostFreqFile.Read(buffer, 0, wordLength);
                string word = Encoding.ASCII.GetString(buffer);

                // Read the frequency.
                buffer = new byte[8];
                statFile.Read(buffer, 0, buffer.Length);
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(buffer);
                double frequency = BitConverter.ToDouble(buffer, 0);

                // Add to the dictionnary.
                ProportionDocContaining10MostFrequent.Add(word, frequency);
            }

            buffer = new byte[8];
            statFile.Read(buffer, 0, buffer.Length);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(buffer);
            IndexSizeInMemory = BitConverter.ToInt64(buffer, 0);

        }
    }
}
