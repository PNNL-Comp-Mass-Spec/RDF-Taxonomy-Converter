using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using PRISM;

namespace RDF_Taxonomy_Converter;

internal class RDFTaxonomyProcessor : EventNotifier
{
    /// <summary>
    /// Default primary key suffix
    /// </summary>
    /// <remarks>
    /// This suffix is appended to the taxonomy term identifier when creating the primary key for the Term_PK column
    /// </remarks>
    public const string DEFAULT_PRIMARY_KEY_SUFFIX = "NEWT1";

    private string NullValueFlag { get; }

    /// <summary>
    /// Processing options
    /// </summary>
    public RDFTaxonomyProcessorOptions Options { get; set; }

    /// <summary>
    /// String appended to the taxonomy term identifier when creating the primary key for the Term_PK column
    /// </summary>
    public string PrimaryKeySuffix { get; }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="options">Processing options</param>
    /// <param name="primaryKeySuffix">Primary key suffix</param>
    public RDFTaxonomyProcessor(RDFTaxonomyProcessorOptions options, string primaryKeySuffix = DEFAULT_PRIMARY_KEY_SUFFIX)
    {
        Options = options;
        NullValueFlag = GetNullValueFlag();
        PrimaryKeySuffix = string.IsNullOrWhiteSpace(primaryKeySuffix) ? string.Empty : primaryKeySuffix;
    }

    /// <summary>
    /// Process the given RDF file
    /// </summary>
    /// <param name="inputFilePath">RDF taxonomy file path</param>
    /// <returns>True if successful, false if an error</returns>
    public bool ProcessFile(string inputFilePath)
    {
        try
        {
            var inputFile = new FileInfo(inputFilePath);
            if (!inputFile.Exists)
            {
                OnWarningEvent("File not found: " + inputFile.FullName);
                return false;
            }

            OnStatusEvent("Input file:  " + inputFile.FullName);

            string outputFilePath;

            if (string.IsNullOrWhiteSpace(Options.OutputFilePath))
            {
                var defaultOutputFileName = Path.GetFileNameWithoutExtension(inputFile.Name) + "_info.txt";

                if (inputFile.Directory == null)
                {
                    OnWarningEvent("Unable to determine the parent directory of the input file: " + inputFile.FullName);
                    outputFilePath = defaultOutputFileName;
                }
                else
                {
                    outputFilePath = Path.Combine(inputFile.Directory.FullName, defaultOutputFileName);
                }
            }
            else
            {
                outputFilePath = Options.OutputFilePath;
            }

            var outputFile = new FileInfo(outputFilePath);
            OnStatusEvent("Output file: " + outputFile.FullName);

            if (outputFile.Exists)
            {
                OnWarningEvent("Existing file will be overwritten");
            }

            return ConvertRdfTaxonomyFile(inputFile, outputFile);
        }
        catch (Exception ex)
        {
            OnErrorEvent("Error occurred in RDFTaxonomyProcessor->ProcessFile", ex);
            return false;
        }
    }

    private void AppendAlternateNames(ICollection<string> lineOut, TaxonomyEntry entry)
    {
        if (Options.IncludeCommonName)
        {
            lineOut.Add(GetValueOrNull(entry.CommonName));
        }

        if (Options.IncludeSynonym)
        {
            lineOut.Add(GetValueOrNull(entry.Synonym));
        }

        if (Options.IncludeMnemonic)
        {
            lineOut.Add(GetValueOrNull(entry.Mnemonic));
        }
    }

    private static byte BoolToTinyInt(bool value)
    {
        if (value)
            return 1;

        return 0;
    }

    private bool ConvertRdfTaxonomyFile(FileSystemInfo inputFile, FileInfo outputFile)
    {
        try
        {
            // Read the taxonomy entries from the RDF file
            // Track them using this dictionary, where keys are identifier IDs
            var taxonomyEntries = new Dictionary<int, TaxonomyEntry>();

            var rdfDescriptionCount = 0;

            // Read the input file using a forward-only XML reader

            using var xmlReader = new XmlTextReader(new FileStream(inputFile.FullName, FileMode.Open, FileAccess.Read, FileShare.Read));

            var currentIdentifier = string.Empty;
            var parseNextRdfType = false;

            var startTime = DateTime.UtcNow;
            var lastStatus = startTime;

            while (xmlReader.Read())
            {
                switch (xmlReader.NodeType)
                {
                    case XmlNodeType.Element:
                        // Start element

                        switch (xmlReader.Name)
                        {
                            case "rdf:Description":
                                rdfDescriptionCount++;

                                if (!xmlReader.HasAttributes)
                                {
                                    OnWarningEvent("rdf:Description {0} does not have any attributes; skipping", rdfDescriptionCount);
                                    break;
                                }

                                if (!GetAttributeValue(xmlReader, "rdf:about", out currentIdentifier))
                                {
                                    OnWarningEvent("rdf:Description {0} does not have attribute rdf:about", rdfDescriptionCount);
                                    break;
                                }

                                parseNextRdfType = true;
                                break;

                            case "rdf:type":
                                if (!parseNextRdfType)
                                    break;

                                parseNextRdfType = false;

                                if (!xmlReader.HasAttributes)
                                {
                                    OnWarningEvent("rdf:Description {0} is not followed by a 'rdf:type' element with attributes; skipping", rdfDescriptionCount);
                                    break;
                                }

                                if (!GetAttributeValue(xmlReader, "rdf:resource", out var entryType))
                                {
                                    OnWarningEvent("rdf:Description {0}, element 'rdf:type' does not have attribute rdf:resource", rdfDescriptionCount);
                                    break;
                                }

                                // entryType should be of the form "http://purl.uniprot.org/core/Taxon" or "http://purl.uniprot.org/core/Strain"
                                // Extract the text after the last forward slash

                                entryType = GetValueAfterLastSlash(entryType);

                                if (entryType.Equals("Strain", StringComparison.OrdinalIgnoreCase))
                                {
                                    // The entry type is "http://purl.uniprot.org/core/Strain", meaning a strain that does not have an official taxonomy identifier
                                    // We ignore these types of strains
                                    break;
                                }

                                if (entryType.Equals("Image", StringComparison.OrdinalIgnoreCase))
                                {
                                    // The entry type is "http://xmlns.com/foaf/0.1/Image", which we ignore
                                    break;
                                }

                                // Parse out the integer, which is the taxonomy item's ID
                                if (!int.TryParse(currentIdentifier, out var termID))
                                {
                                    OnWarningEvent("rdf:Description {0}, element 'rdf:about' does not have a numeric value: {1}; skipping", rdfDescriptionCount, currentIdentifier);
                                    break;
                                }

                                ParseEntry(xmlReader, taxonomyEntries, termID, rdfDescriptionCount);

                                if (DateTime.UtcNow.Subtract(lastStatus).TotalSeconds >= 2)
                                {
                                    lastStatus = DateTime.UtcNow;
                                    OnStatusEvent("Processed {0:N0} entries; {1}", rdfDescriptionCount, GetElapsedTime(startTime));
                                }

                                break;
                        }

                        break;
                }
            }

            var leafNodeCount = IdentifyLeafTerms(taxonomyEntries);

            Console.WriteLine();
            OnStatusEvent("Found {0:N0} taxonomy entries, of which {1:N0} are leaf nodes", taxonomyEntries.Count, leafNodeCount);
            Console.WriteLine();

            var taxonomyInfoSuccess = WriteTaxonomyInfoToFile(taxonomyEntries, outputFile);

            if (!taxonomyInfoSuccess || !Options.SaveOtherNames)
            {
                Console.WriteLine();

                if (taxonomyInfoSuccess)
                {
                    OnStatusEvent("Conversion is complete");
                    return true;
                }

                OnWarningEvent("Error creating file {0}", PathUtils.CompactPathString(outputFile.FullName, 120));
                return false;
            }

            var otherNamesFileName = string.Format("{0}{1}", Path.GetFileNameWithoutExtension(outputFile.Name), "_OtherNames.txt");

            var otherNamesFile = outputFile.Directory == null
                ? new FileInfo(otherNamesFileName)
                : new FileInfo(Path.Combine(outputFile.Directory.FullName, otherNamesFileName));

            var otherNamesSuccess = WriteOtherNamesToFile(taxonomyEntries, otherNamesFile);

            Console.WriteLine();

            if (otherNamesSuccess)
            {
                OnStatusEvent("Conversion is complete");
                return true;
            }

            OnWarningEvent("Error creating file {0}", PathUtils.CompactPathString(otherNamesFile.FullName, 120));
            return false;
        }
        catch (Exception ex)
        {
            OnErrorEvent("Error occurred in ConvertRdfTaxonomyFile", ex);
            return false;
        }
    }

    private bool GetAncestor(IReadOnlyDictionary<int, TaxonomyEntry> taxonomyEntries, int ancestorID, out TaxonomyEntry ancestor)
    {
        if (taxonomyEntries.TryGetValue(ancestorID, out var ancestorMatch))
        {
            ancestor = ancestorMatch;
            return true;
        }

        if (ancestorID == 1)
        {
            ancestor = new TaxonomyEntry(1, GetNullValueFlag())
            {
                Name = "root"
            };

            return true;
        }

        ancestor = null;
        return false;
    }

    private bool GetAttributeValue(XmlReader xmlReader, string attributeName, out string attributeValue)
    {
        xmlReader.MoveToFirstAttribute();

        do
        {
            if (xmlReader.Name == attributeName)
            {
                attributeValue = xmlReader.Value;
                return true;
            }
        } while (xmlReader.MoveToNextAttribute());

        attributeValue = string.Empty;

        return false;
    }

    private string GetElapsedTime(DateTime startTime)
    {
        var totalSeconds = DateTime.UtcNow.Subtract(startTime).TotalSeconds;

        if (totalSeconds <= 60)
            return string.Format("{0:F0} seconds elapsed", totalSeconds);

        var totalMinutes = totalSeconds / 60.0;

        return string.Format("{0:F1} minutes elapsed", totalMinutes);
    }

    private string GetElementValue(XmlReader xmlReader)
    {
        while (xmlReader.Read())
        {
            switch (xmlReader.NodeType)
            {
                case XmlNodeType.Element:
                    // This is unexpected, but we'll support it
                    break;

                case XmlNodeType.EndElement:
                    return string.Empty;

                case XmlNodeType.Text:
                    return xmlReader.Value;
            }
        }

        return string.Empty;
    }

    private string GetNullValueFlag()
    {
        return Options.FormatForPostgres ? @"\N" : string.Empty;
    }

    private static string GetValueAfterLastSlash(string text)
    {
        var lastSlashIndex = text.LastIndexOf('/');

        return lastSlashIndex < 0 ? text : text.Substring(lastSlashIndex + 1);
    }

    private string GetValueOrNull(string text)
    {
        return string.IsNullOrWhiteSpace(text) ? NullValueFlag : text;
    }

    private int IdentifyLeafTerms(Dictionary<int, TaxonomyEntry> taxonomyEntries)
    {
        // Make a list of identifiers that are parents of other terms
        var parentNodes = new SortedSet<int>();

        var leafNodeCount = 0;

        foreach (var entry in taxonomyEntries.Values)
        {
            parentNodes.Add(entry.ParentTermID);
        }

        // Update IsLeaf for the taxonomy entries
        // An entry is a leaf node if no other nodes reference it as a parent
        foreach (var entry in taxonomyEntries)
        {
            if (parentNodes.Contains(entry.Value.Identifier))
                continue;

            entry.Value.IsLeaf = true;
            leafNodeCount++;
        }

        return leafNodeCount;
    }

    /// <summary>
    /// Parse a rdf:Description entry
    /// </summary>
    /// <param name="xmlReader">XML reader</param>
    /// <param name="taxonomyEntries">Dictionary of taxonomy entries (keys are integer IDs, values are the taxonomy info</param>
    /// <param name="identifier">Taxonomy term identifier</param>
    /// <param name="rdfDescriptionNumber">Current count of the number of rdf:Description entries processed</param>
    private void ParseEntry(XmlReader xmlReader, IDictionary<int, TaxonomyEntry> taxonomyEntries, int identifier, int rdfDescriptionNumber)
    {
        var taxonomyTerm = new TaxonomyEntry(identifier, NullValueFlag);
        taxonomyEntries.Add(identifier, taxonomyTerm);

        while (xmlReader.Read())
        {
            switch (xmlReader.NodeType)
            {
                case XmlNodeType.Whitespace:
                case XmlNodeType.Comment:
                    // Ignore these
                    break;

                case XmlNodeType.Element:
                    // Start element

                    switch (xmlReader.Name)
                    {
                        case "rank":
                            if (!GetAttributeValue(xmlReader, "rdf:resource", out var taxonomyRank))
                            {
                                OnWarningEvent("rdf:Description {0}, element 'rank' does not have attribute 'rdf:resource'", rdfDescriptionNumber);
                                break;
                            }

                            // taxonomyRank should be of the form "http://purl.uniprot.org/core/Species"
                            // Extract the text after the last forward slash

                            taxonomyTerm.Rank = GetValueAfterLastSlash(taxonomyRank);
                            break;

                        case "mnemonic":
                            taxonomyTerm.Mnemonic = GetElementValue(xmlReader);
                            break;

                        case "scientificName":
                            taxonomyTerm.Name = GetElementValue(xmlReader);
                            break;

                        case "commonName":
                            taxonomyTerm.CommonName = GetElementValue(xmlReader);
                            break;

                        case "synonym":
                            taxonomyTerm.Synonym = GetElementValue(xmlReader);
                            break;

                        case "otherName":
                            var otherName = GetElementValue(xmlReader);
                            taxonomyTerm.AddAlternateName(otherName);
                            break;

                        case "rdfs:subClassOf":
                            if (!GetAttributeValue(xmlReader, "rdf:resource", out var parentID))
                            {
                                OnWarningEvent("rdf:Description {0} does not have attribute 'rdf:resource'", rdfDescriptionNumber);
                                break;
                            }

                            // Check for parentID referencing "http://purl.uniprot.org/core/Taxon"
                            // Using IndexOf in case they switch from "http://" to "https://"

                            if (parentID.IndexOf("purl.uniprot.org/core/Taxon", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                // Term name is at the root level (cellular organisms, other sequences, unclassified sequences, viruses)
                                taxonomyTerm.ParentTermID = 1;
                                break;
                            }

                            // Parse out the integer, which is the parent taxonomy item's ID
                            if (!int.TryParse(parentID, out var parentTermID))
                            {
                                OnWarningEvent("rdf:Description {0}, element 'rdfs:subClassOf' does not have a numeric value for the 'rdf:resource' attribute: {1}", rdfDescriptionNumber, parentID);
                                break;
                            }

                            taxonomyTerm.ParentTermID = parentTermID;
                            break;
                    }

                    break;

                case XmlNodeType.EndElement:
                    if (xmlReader.Name.Equals("rdf:Description"))
                        return;

                    break;

            }
        }
    }

    private List<string> TaxonomyTermNoParents(TaxonomyEntry taxonomyTerm)
    {
        var suffix = string.IsNullOrWhiteSpace(PrimaryKeySuffix) ? string.Empty : PrimaryKeySuffix;

        var dataColumns = new List<string>
        {
            taxonomyTerm.Identifier + suffix,               // Term Primary Key
            taxonomyTerm.Name,                              // Term Name
            taxonomyTerm.Identifier.ToString(),             // Term Identifier
            BoolToTinyInt(taxonomyTerm.IsLeaf).ToString()   // Is_Leaf
        };

        if (Options.IncludeRank)
        {
            dataColumns.Add(string.IsNullOrWhiteSpace(taxonomyTerm.Rank) ? NullValueFlag : taxonomyTerm.Rank);
        }

        return dataColumns;
    }

    private List<string> TaxonomyTermWithParents(TaxonomyEntry taxonomyTerm, TaxonomyEntry parentTerm)
    {
        var dataColumns = TaxonomyTermNoParents(taxonomyTerm);

        if (parentTerm == null)
        {
            dataColumns.Add(NullValueFlag);     // Parent term name
            dataColumns.Add(NullValueFlag);     // Parent term identifier
        }
        else
        {
            dataColumns.Add(parentTerm.Name);                  // Parent term name
            dataColumns.Add(parentTerm.Identifier.ToString()); // Parent term identifier
        }

        return dataColumns;
    }

    private void WriteLine(TextWriter writer, IList<string> lineOut, int columnCount, string nullValueFlag)
    {
        if (!string.IsNullOrWhiteSpace(nullValueFlag))
        {
            while (lineOut.Count < columnCount)
            {
                lineOut.Add(nullValueFlag);
            }
        }

        if (Options.FormatForPostgres)
        {
            for (var i = 0; i < lineOut.Count; i++)
            {
                if (lineOut[i].Equals(@"\N"))
                    continue;

                // Escape backslashes with \\
                lineOut[i] = lineOut[i].Replace(@"\", @"\\");
            }
        }

        writer.WriteLine(string.Join("\t", lineOut));
    }

    private bool WriteOtherNamesToFile(
        Dictionary<int, TaxonomyEntry> taxonomyEntries,
        FileSystemInfo outputFile)
    {
        try
        {
            OnStatusEvent("Creating " + outputFile.FullName);

            var yearMatcher = new Regex(@" [1-2]\d{3}\b", RegexOptions.Compiled);

            using var writer = new StreamWriter(new FileStream(outputFile.FullName, FileMode.Create, FileAccess.Write, FileShare.ReadWrite));

            var columnHeaders = new List<string>
                {
                    "Identifier",
                    "Other_Name"
                };

            writer.WriteLine(string.Join("\t", columnHeaders));

            var columnCount = columnHeaders.Count;
            var lineOut = new List<string>();

            foreach (var entry in taxonomyEntries.Values)
            {
                lineOut.Clear();
                lineOut.Add(entry.Identifier.ToString());
                lineOut.Add(string.Empty);

                var firstNameWritten = false;

                foreach (var otherName in entry.OtherNames)
                {
                    if (firstNameWritten)
                    {
                        // ReSharper disable CommentTypo

                        // Check for cases where the current value of otherName starts with the same text as the previously written name, but the current entry contains a year
                        // Examples:
                        //   "Agromonas" and "Agromonas Ohta and Hattori 1985"
                        //   "Sarcobium" and "Sarcobium Drozanski 1991"
                        //   "Eriophorum crinigerum (A.Gray) Beetle" and "Eriophorum crinigerum (A.Gray) Beetle, 1942"
                        //   "Francisella tularensis subsp. novicida" and "Francisella tularensis subsp. novicida (Larson et al. 1955) Huber et al. 2010"

                        // ReSharper restore CommentTypo

                        // When near-duplicates like those shone above are found, skip the second entry
                        if (otherName.StartsWith(lineOut[1]) && yearMatcher.IsMatch(otherName))
                        {
                            continue;
                        }
                    }
                    else
                    {
                        firstNameWritten = true;
                    }

                    lineOut[1] = otherName;
                    WriteLine(writer, lineOut, columnCount, NullValueFlag);
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            OnErrorEvent("Error occurred in WriteOtherNamesToFile", ex);
            return false;
        }
    }

    private bool WriteTaxonomyInfoToFile(
            Dictionary<int, TaxonomyEntry> taxonomyEntries,
            FileSystemInfo outputFile)
    {
        try
        {
            OnStatusEvent("Creating " + outputFile.FullName);

            using var writer = new StreamWriter(new FileStream(outputFile.FullName, FileMode.Create, FileAccess.Write, FileShare.ReadWrite));

            var columnHeaders = new List<string>
                {
                    "Term_PK",
                    "Term_Name",
                    "Identifier",
                    "Is_Leaf"
                };

            if (Options.IncludeRank)
            {
                columnHeaders.Add("Rank");
            }

            if (Options.IncludeGrandparentTerms && !Options.IncludeParentTerms)
            {
                // Force-enable inclusion of parent terms because grandparent terms will be included
                Options.IncludeParentTerms = true;
            }

            if (Options.IncludeParentTerms)
            {
                columnHeaders.Add("Parent_Term_Name");
                columnHeaders.Add("Parent_Term_ID");
            }

            if (Options.IncludeGrandparentTerms)
            {
                columnHeaders.Add("Grandparent_Term_Name");
                columnHeaders.Add("Grandparent_Term_ID");
            }

            if (Options.IncludeCommonName)
            {
                columnHeaders.Add("Common_Name");
            }

            if (Options.IncludeSynonym)
            {
                columnHeaders.Add("Synonym");
            }
            if (Options.IncludeMnemonic)
            {
                columnHeaders.Add("Mnemonic");
            }

            writer.WriteLine(string.Join("\t", columnHeaders));

            var columnCount = columnHeaders.Count;
            var lineOut = new List<string>();

            foreach (var entry in taxonomyEntries.Values)
            {
                lineOut.Clear();

                if (entry.ParentTermID == 0 || !Options.IncludeParentTerms)
                {
                    lineOut.AddRange(TaxonomyTermNoParents(entry));

                    if (Options.IncludeParentTerms)
                    {
                        lineOut.Add(NullValueFlag);     // Parent term name
                        lineOut.Add(NullValueFlag);     // Parent term ID

                        if (Options.IncludeGrandparentTerms)
                        {
                            lineOut.Add(NullValueFlag); // Grandparent term name
                            lineOut.Add(NullValueFlag); // Grandparent term ID
                        }
                    }

                    AppendAlternateNames(lineOut, entry);

                    WriteLine(writer, lineOut, columnCount, NullValueFlag);
                    continue;
                }

                var ancestorFound = GetAncestor(taxonomyEntries, entry.ParentTermID, out var parentTerm);

                if (!ancestorFound || parentTerm.ParentTermID < 1 || !Options.IncludeGrandparentTerms)
                {
                    // No grandparents (or grandparents are disabled)
                    lineOut.AddRange(TaxonomyTermWithParents(entry, parentTerm));

                    if (Options.IncludeGrandparentTerms)
                    {
                        lineOut.Add(NullValueFlag); // Grandparent term name
                        lineOut.Add(NullValueFlag); // Grandparent term ID
                    }

                    AppendAlternateNames(lineOut, entry);

                    WriteLine(writer, lineOut, columnCount, NullValueFlag);
                    continue;
                }

                var grandparentFound = GetAncestor(taxonomyEntries, parentTerm.Identifier, out var grandparentTerm);

                lineOut.AddRange(TaxonomyTermWithParents(entry, parentTerm));

                if (grandparentFound)
                {
                    lineOut.Add(grandparentTerm.Name);                  // Grandparent term name
                    lineOut.Add(grandparentTerm.Identifier.ToString()); // Grandparent term ID
                }
                else
                {
                    lineOut.Add(NullValueFlag);     // Grandparent term name
                    lineOut.Add(NullValueFlag);     // Grandparent term identifier
                }

                AppendAlternateNames(lineOut, entry);

                WriteLine(writer, lineOut, columnCount, NullValueFlag);
            }

            return true;
        }
        catch (Exception ex)
        {
            OnErrorEvent("Error occurred in WriteTaxonomyInfoToFile", ex);
            return false;
        }
    }
}