using PRISM;

namespace RDF_Taxonomy_Converter;

internal class RDFTaxonomyProcessorOptions
{
    // Ignore Spelling: Postgres

    public const string PROGRAM_DATE = "June 5, 2024";

    /// <summary>
    /// Input file path
    /// </summary>
    /// <remarks>.xml file</remarks>
    [Option("InputFilePath", "I",
        ArgPosition = 1, Required = true, HelpShowsDefault = false, IsInputFilePath = true,
        HelpText = "The name of the input file to process. " +
                   "When using /I at the command line, surround the filename with double quotes if it contains spaces")]
    public string InputFilePath { get; set; }

    /// <summary>
    /// Output file path
    /// </summary>
    [Option("OutputFilePath", "OutputFile", "O", HelpShowsDefault = false,
        HelpText = "Output file name (or full path). " +
                   "If omitted, the output file will be auto-named, and created in the same directory as the input file")]
    public string OutputFilePath { get; set; }

    [Option("IncludeRank", "Rank", HelpShowsDefault = false,
        HelpText = "When true, include classification rank (family, genus, species, etc.) in the output")]
    public bool IncludeRank { get; set; } = true;

    [Option("IncludeParentTerms", "IncludeParents", HelpShowsDefault = false,
        HelpText = "When true, include columns Parent_Term_Name and Parent_Term_ID in the output")]
    public bool IncludeParentTerms { get; set; } = true;

    [Option("IncludeGrandparentTerms", "IncludeGrandparents", HelpShowsDefault = false,
        HelpText = "When true, include columns Grandparent_Term_Name and Grandparent_Term_ID in the output")]
    public bool IncludeGrandparentTerms { get; set; } = true;

    [Option("IncludeCommonName", "CommonName", HelpShowsDefault = false,
        HelpText = "When true, include the common name (if defined)")]
    public bool IncludeCommonName { get; set; } = true;

    [Option("IncludeSynonym", "Synonym", HelpShowsDefault = false,
        HelpText = "When true, include the synonym (if defined)")]
    public bool IncludeSynonym { get; set; } = true;

    [Option("IncludeMnemonic", "Mnemonic", HelpShowsDefault = false,
        HelpText = "When true, include the mnemonic name (a five letter abbreviation of the scientific name)")]
    public bool IncludeMnemonic { get; set; } = true;


    [Option("FormatForPostgres", "Postgres", HelpShowsDefault = false,
        HelpText = "When true, use \\N for null values (empty columns in the output file), " +
                   "escape backslashes, and replace double quotes with \"\". " +
                   "This allows the data file to be imported using the COPY command: " +
                   "COPY ont.t_tmp_newt FROM '/tmp/taxonomy_info.txt' CSV HEADER DELIMITER E'\\t' QUOTE '\"'")]
    public bool FormatForPostgres { get; set; }

    /// <summary>
    /// Validate the options
    /// </summary>
    /// <returns>True if all options are valid</returns>
    public bool Validate()
    {
        if (string.IsNullOrWhiteSpace(InputFilePath))
        {
            ConsoleMsgUtils.ShowError("Error: Input path must be provided and non-empty; \"{0}\" was provided", InputFilePath);
            return false;
        }

        return true;
    }
}