"""Generate non-sensitive Explorer/UI fixtures; never use private user files in captures."""
from pathlib import Path
from zipfile import ZipFile

root = Path(__file__).resolve().parent.parent
destination = root / "artifacts" / "manual-fixtures"
destination.mkdir(parents=True, exist_ok=True)
with ZipFile(destination / "Konvert sample.docx", "w") as archive:
    archive.writestr("[Content_Types].xml", '''<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/></Types>''')
    archive.writestr("_rels/.rels", '''<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/></Relationships>''')
    archive.writestr("word/document.xml", '''<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body><w:p><w:r><w:rPr><w:b/><w:sz w:val="36"/><w:color w:val="2463A5"/></w:rPr><w:t>Konvert conversion test</w:t></w:r></w:p><w:p><w:r><w:t>A local document selected in Explorer.</w:t></w:r></w:p><w:p><w:r><w:t>Original content stays untouched.</w:t></w:r></w:p></w:body></w:document>''')
(destination / "Konvert sample.csv").write_text('name,quantity\n"Example, one",2\n', encoding="utf-8")
print(destination)
