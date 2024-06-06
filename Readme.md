# RDF Taxonomy Converter

This program processes an RDF taxonomy file and creates a tab-delimited text file with the extracted taxonomy terms
* The RDF taxonomy file can be downloaded from ftp.uniprot.org (see below)
* The RDF file is an XML representation of the taxonomy names browsable at https://www.uniprot.org/taxonomy/

## Console Switches

The RDF Taxonomy Converter is a console application, and must be run from the Windows command prompt.

```
RDF_Taxonomy_Converter.exe
 InputFilePath [/O:OutputFilePath]
 [/IncludeRank]
 [/IncludeParents] [/IncludeGrandparents] 
 [/CommonName] [/Synonym] [/Mnemonic] [/OtherNames]
 [/Postgres]
 [/ParamFile:ParamFileName.conf] [/CreateParamFile]
```

InputFilePath is a path to the input file
* Optionally, instead use `/I:InputFilePath` to specify the file name
* The normal extension for an RDF taxonomy file is `.rdf`
* See below for an excerpt from an example input file

Use `/O` or `-O` to specify the output file path

Use `/IncludeRank:False` to disable including the classification rank (family, genus, species, etc.) in the output

Use `/IncludeParents:False` to disable including columns Parent_Term_Name and Parent_Term_ID in the output

Use `/IncludeGrandparents:False` to disable including columns Grandparent_Term_Name and Grandparent_Term_ID in the output

Use `/CommonName:False` to disable including the common name

Use `/Synonym:False` to disable including the synonym

Use `/IncludeMnemonic:False` to disable including the mnemonic name (a five letter abbreviation of the scientific name)

Use `/OtherNames:False` to disable creating a file listing other names for the taxonomy terms

Use `/Postgres` to use \N for null values (empty columns in the output file), escape backslashes, and replace double quotes with ""
* This allows the data file to be imported using the COPY command, e.g.
```
COPY ont.t_tmp_newt FROM '/tmp/taxonomy_info.txt' CSV HEADER DELIMITER E'\t' QUOTE '"'
```

The processing options can be specified in a parameter file using `/ParamFile:Options.conf` or `/Conf:Options.conf`
* Define options using the format `ArgumentName=Value`
* Lines starting with `#` or `;` will be treated as comments
* Additional arguments on the command line can supplement or override the arguments in the parameter file

Use `/CreateParamFile` to create an example parameter file
* By default, the example parameter file content is shown at the console
* To create a file named Options.conf, use `/CreateParamFile:Options.conf`

## Downloading and Processing UniProt Taxonomy Terms

* Use FileZilla (or WinSCP) to connect to ftp.uniprot.org
* Navigate to /pub/databases/uniprot/current_release/rdf
* Download file `taxonomy.rdf.xz`
* Use 7-Zip to extract the .rdf file
* Process the `.rdf` file using RDF_Taxonomy_Converter.exe

### Example Input File Excerpt

```xml
<?xml version='1.0' encoding='UTF-8'?>
<rdf:RDF xml:base="http://purl.uniprot.org/taxonomy/" xmlns="http://purl.uniprot.org/core/" xmlns:foaf="http://xmlns.com/foaf/0.1/" xmlns:owl="http://www.w3.org/2002/07/owl#" xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#" xmlns:rdfs="http://www.w3.org/2000/01/rdf-schema#" xmlns:skos="http://www.w3.org/2004/02/skos/core#">
<owl:Ontology rdf:about="">
<owl:imports rdf:resource="http://purl.uniprot.org/core/"/>
</owl:Ontology>
<rdf:Description rdf:about="10239">
<rdf:type rdf:resource="http://purl.uniprot.org/core/Taxon"/>
<rank rdf:resource="http://purl.uniprot.org/core/Superkingdom"/>
<mnemonic>9VIRU</mnemonic>
<scientificName>Viruses</scientificName>
<otherName>Vira</otherName>
<otherName>Viridae</otherName>
<rdfs:subClassOf rdf:resource="http://purl.uniprot.org/core/Taxon"/>
<rdf:Description rdf:about="10090">
<rdf:type rdf:resource="http://purl.uniprot.org/core/Taxon"/>
<rank rdf:resource="http://purl.uniprot.org/core/Species"/>
<replaces rdf:resource="85055"/>
<mnemonic>MOUSE</mnemonic>
<scientificName>Mus musculus</scientificName>
<commonName>Mouse</commonName>
<otherName>LK3 transgenic mice</otherName>
<otherName>Mus musculus Linnaeus, 1758</otherName>
<otherName>Mus sp. 129SV</otherName>
<otherName>house mouse</otherName>
<rdfs:subClassOf rdf:resource="862507"/>
<rdf:Description rdf:about="10090#strain-020">
<rdf:type rdf:resource="http://purl.uniprot.org/core/Strain"/>
<name>020</name>
</rdf:Description>
```

## Contacts

Written by Matthew Monroe for PNNL (Richland, WA) \
E-mail: matthew.monroe@pnnl.gov or proteomics@pnnl.gov \
Website: https://github.com/PNNL-Comp-Mass-Spec/ or https://www.pnnl.gov/integrative-omics

## License

The RDF Taxonomy Converter is licensed under the 2-Clause BSD License; 
you may not use this program except in compliance with the License. You may obtain 
a copy of the License at https://opensource.org/licenses/BSD-2-Clause

Copyright 2024 Battelle Memorial Institute
