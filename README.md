# Scrapelr.NET

Scrapelr.NET is a simple web scraper designed to collect a set of web pages into a single PDF or markdown file.
You can input a text file with a list of pages to be included or provide a starting URL, a depth parameter, and 
a few other options (listed below) to let the crawler work automatically.

## Usage

```
sn [options]

Options:
-r, --root <root>                         The URL to start crawling from. Required unless --inputFile is given.
-i, --inputFile <inputFile>               A text file of URLs to start crawling from, one per line. Blank lines and lines starting with # are ignored. Use instead of --root.
-o, --outputFile <outputFile> (REQUIRED)  Where to save the merged document. Existing files are overwritten.
-f, --format <pdf|markdown>               pdf or markdown. [default: markdown if --outputFile ends in .md or .markdown, otherwise pdf]
--fullPage                                Convert whole pages to Markdown, including navigation and footers, rather than just their main content.
-m, --maxDepth <maxDepth>                 How many links deep to follow from each starting page, which is depth 0. [default: 1, or 0 with --inputFile]
-p, --pathOnly                            Only follow links beneath the starting URL's path. Implies --domainOnly.
-d, --domainOnly                          Only follow links on the starting URL's scheme, host and port.
-?, -h, --help                            Show help and usage information
--version
```

## Downloads

[Releases](https://github.com/cmpnnt/Scrapelr.NET/releases)

## Notes

1. The PDF version is ugly because it uses a headless Chrome's "print to PDF" feature. It was only ever intended as
a supplement to the markdown output, anyway.
2. Most of this was hand-coded, so there might be some bugs I haven't encountered yet. If you find one, report it.
3. The MacOS version is not signed, so you'll have to run `xattr -r -d com.apple.quarantine sn` until I feel like jumping through Apple's hoops.
4. If you'll want to use it on a regular basis, consider putting it on your PATH or as an alias in your bashrc/zshrc: `alias sn="/path/to/sn"`

## Motivation

1. To reduce the number of sources counted against your AI agent's limit by consolidating multiple web pages into a single file.
2. As a toy practice project.