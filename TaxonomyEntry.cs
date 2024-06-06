using System.Collections.Generic;

namespace RDF_Taxonomy_Converter
{
    internal class TaxonomyEntry
    {
        // Ignore Spelling: RDF

        /// <summary>
        /// Taxonomy entry identifier
        /// </summary>
        public int Identifier { get; }

        // ReSharper disable CommentTypo
        /// <summary>
        /// Mnemonic, which is a five letter abbreviation of the scientific name
        /// </summary>
        /// <remarks>
        /// Examples:
        /// MOUSE: Mus musculus
        /// HUMAN: Homo sapiens
        /// FELCA: Felis catus (domestic cat)
        /// CANLF: Canis lupus familiaris (dog)
        /// CANLU: Canis lupus (gray wolf)
        /// </remarks>
        // ReSharper restore CommentTypo
        public string Mnemonic { get; set; }

        /// <summary>
        /// Scientific name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Common name
        /// </summary>
        public string CommonName { get; set; }

        /// <summary>
        /// Taxonomic rank
        /// </summary>
        /// <remarks>Superkingdom, family, genus, species, etc.</remarks>
        public string Rank { get; set; }

        /// <summary>
        /// Synonym
        /// </summary>
        public string Synonym { get; set; }

        /// <summary>
        /// Other names
        /// </summary>
        public SortedSet<string> OtherNames { get; }

        /// <summary>
        /// True if this taxonomy entry does not have any children
        /// </summary>
        public bool IsLeaf { get; set; }

        /// <summary>
        /// Parent term ID
        /// </summary>
        /// <remarks>1 if the term is at the root level (cellular organisms, other sequences, unclassified sequences, viruses)</remarks>
        public int ParentTermID { get; set; }

        // ReSharper disable once CommentTypo

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="identifier">Taxonomy term ID</param>
        /// <param name="nullValueFlag">Null value flag (empty string for SQL Server; \N for PostgreSQL)</param>
        public TaxonomyEntry(int identifier, string nullValueFlag = "")
        {
            Identifier = identifier;
            Name = nullValueFlag;
            CommonName = nullValueFlag;
            Rank = nullValueFlag;
            Synonym = nullValueFlag;
            OtherNames = new SortedSet<string>();
            IsLeaf = false;
            ParentTermID = 0;
        }

        /// <summary>
        /// Add alternative name
        /// </summary>
        /// <param name="otherName">Alternative name</param>
        public void AddAlternateName(string otherName)
        {
            if (string.IsNullOrWhiteSpace(otherName))
                return;

            OtherNames.Add(otherName);
        }

        /// <summary>
        /// Show identifier and name
        /// </summary>
        public override string ToString()
        {
            return Identifier + ": " + Name;
        }
    }
}
