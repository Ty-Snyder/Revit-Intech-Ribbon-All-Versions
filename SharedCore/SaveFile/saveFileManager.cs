﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SharedCore.SaveFile
{
    public class SaveFileManager
    {
        private string filePath;
        private ISaveFileFormat format;

        public SaveFileManager(string filePath, ISaveFileFormat format)
        {
            this.filePath = Path.GetFullPath(filePath);
            this.format = format ?? throw new ArgumentNullException(nameof(format));
        }

        public SaveFileManager(string filePath)
        {
            this.filePath = Path.GetFullPath(filePath);
            this.format = new TxtFormat() ?? throw new ArgumentNullException(nameof(format));
        }

        public List<SaveFileSection> ReadAllSections()
        {
            var sections = new List<SaveFileSection>();
            if (!File.Exists(filePath)) return sections;

            using (StreamReader sr = new StreamReader(filePath))
            {
                SaveFileSection currentSection = null;
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    if (line.StartsWith("# "))
                    {
                        if (currentSection != null)
                            sections.Add(currentSection);

                        string fullName = line.Substring(2).Trim();
                        string[] parts = fullName.Split(new[] { "::" }, StringSplitOptions.None);
                        string projectName = parts[0];
                        string secondaryName = parts.Length > 1 ? parts[1] : "";

                        currentSection = new SaveFileSection(projectName, secondaryName, "");
                    }
                    else if (currentSection != null && string.IsNullOrEmpty(currentSection.Header))
                    {
                        currentSection.Header = line;
                    }
                    else if (currentSection != null)
                    {
                        currentSection.Rows.Add(format.DeserializeRow(line));
                    }
                }

                if (currentSection != null)
                    sections.Add(currentSection);
            }

            return sections;
        }
        public void WriteAllSections(List<SaveFileSection> sections)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));

            using (StreamWriter sw = new StreamWriter(filePath, false))
            {
                for (int i = 0; i < sections.Count; i++)
                {
                    var section = sections[i];
                    string fullName = section.PrimaryName +
                    (string.IsNullOrEmpty(section.SecondaryName) ? "" : $"::{section.SecondaryName}");

                    sw.WriteLine($"# {fullName}");
                    sw.WriteLine(format.SerializeHeader(section.Header));

                    foreach (var row in section.Rows)
                    {
                        sw.WriteLine(format.SerializeRow(row));
                    }

                    if (i < sections.Count - 1)
                        sw.WriteLine();
                }
            }
            CleanFile();
        }

        private void CleanFile()
        {
            var lines = File.ReadAllLines(filePath)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

            File.WriteAllLines(filePath, lines);
        }

        public void AddOrUpdateSection(SaveFileSection newSection)
        {
            var sections = ReadAllSections();
            var existing = sections.FirstOrDefault(s =>
                s.PrimaryName == newSection.PrimaryName &&
                s.SecondaryName == newSection.SecondaryName);

            if (existing != null)
            {
                existing.Header = newSection.Header;
                existing.Rows = newSection.Rows;
            }
            else
            {
                sections.Add(newSection);
            }
            WriteAllSections(sections);
        }

        public void RemoveSection(string projectName, string secondaryName)
        {
            var sections = ReadAllSections();
            sections.RemoveAll(s =>
                s.PrimaryName == projectName &&
                s.SecondaryName == secondaryName);
            WriteAllSections(sections);
        }

        public List<SaveFileSection> GetSectionsByName(string Name)
        {
            return ReadAllSections().Where(s => s.PrimaryName == Name).ToList();
        }
        public SaveFileSection GetSectionsByName(string PrimaryName, string SecondaryName)
        {
            return ReadAllSections().Where(s => s.PrimaryName == PrimaryName && s.SecondaryName == SecondaryName).FirstOrDefault();
        }
    }

    public class SaveFileSection
    {
        public class NoEmptyList : List<string[]>
        {
            public NoEmptyList() : base() { }
            public NoEmptyList(int capacity) : base(capacity) { }
            public new void Add(string[] item)
            {
                if (item != null || item.Length != 0)
                base.Add(item);
            }
        }
        public string PrimaryName { get; set; }
        public string SecondaryName { get; set; }
        public string Header { get; set; }
        public NoEmptyList Rows { get; set; } = new NoEmptyList();
        public List<string> GetColumn(int columnIndex)
        {
            var column = new List<string>();

            foreach (var row in Rows)
            {
                if (row != null && columnIndex >= 0 && columnIndex < row.Length)
                {
                    column.Add(row[columnIndex]);
                }
                else
                {
                    column.Add(string.Empty);
                }
            }

            return column;
        }

        public List<string[]> lookUp(int columnIndex, string lookup)
        {
            List<string> values = GetColumn(columnIndex);
            List<int> t = new List<int>() ;
            for (int i = 0; i < values.Count(); i++)
            {
                if (string.Equals(values[i], lookup))
                {
                    t.Add(i);
                }
            }
            List<string[]> rows = new List<string[]>();
            foreach (int i in t)
            {
                rows.Add(Rows[i]);
            }
            return rows;
        }

        public SaveFileSection(string projectName, string secondaryName, string header)
        {
            PrimaryName = projectName;
            SecondaryName = secondaryName;
            Header = header;
        }
    }
}
