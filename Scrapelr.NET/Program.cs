using Scrapelr.NET.Cli;

/*
 * --root, -r
 * --pathOnly, -p (Default: true. `true` implies `domainOnly = true`, it's an error to set `domainOnly = false` if `pathOnly = true`)
 *   Note: --pathOnly does strict path checking, so the child URL must be a substring of the parentURL, and the parentURL must be navigable (meaning not 404)
 * --domainOnly, -d (Default: true)
 * --inputFile, -i (TODO)
 * --outputFile -o (mandatory, error if not set)
 * --maxDepth -m (default 1)
 */

/* TODO: We need a flag to specify that the crawler should follow all URLs in an input text file.
 This will be false by default, and will be PathOnly by default. You can specify otherwise. So, if
 you provide a text file with a list of URLs and no other flags, it'll just give you a PDF with
 those pages. */

/* TODO: Consider setting this up with dependency injection. Collector and Processor both implement an
   interface now, so either can be swapped out. */

return await CrawlCommand.Create().Parse(args).InvokeAsync();
