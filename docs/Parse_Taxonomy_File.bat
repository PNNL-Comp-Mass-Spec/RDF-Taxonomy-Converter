@echo off

echo About to process file taxonomy_excerpt.rdf
pause

echo %date% %time%
..\bin\RDF_Taxonomy_Converter.exe taxonomy_excerpt.rdf
..\bin\RDF_Taxonomy_Converter.exe taxonomy_excerpt.rdf /O:taxonomy_info_pg.txt /Postgres
rem ..\bin\RDF_Taxonomy_Converter.exe taxonomy_excerpt.rdf /O:taxonomy_info_terms_with_rank.txt /IncludeParents:False /IncludeGrandparents:False /IncludeMnemonic:False /IncludeRank:True
rem ..\bin\RDF_Taxonomy_Converter.exe taxonomy_excerpt.rdf /O:taxonomy_info_terms_only.txt /IncludeParents:False /IncludeGrandparents:False /IncludeMnemonic:False /IncludeRank:False

echo %date% %time%