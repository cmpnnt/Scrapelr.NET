using Scrapelr.NET.Cli;

/*
 * --root, -r (required unless --inputFile is given)
 * --inputFile, -i (a text file of URLs, one per line; used instead of --root. --maxDepth defaults to 0,
 *   so with no other flags the PDF holds just the listed pages)
 * --pathOnly, -p (Default: true. `true` implies `domainOnly = true`, it's an error to set `domainOnly = false` if `pathOnly = true`)
 *   Note: --pathOnly does strict path checking, so the child URL must be a substring of the parentURL, and the parentURL must be navigable (meaning not 404)
 * --domainOnly, -d (Default: true)
 * --outputFile -o (mandatory, error if not set)
 * --maxDepth -m (default 1, or 0 with --inputFile)
 *
 * The composition root is CrawlCommand: it builds a Pipeline from ICollector and IProcessor factories,
 * so either can be swapped out.
 */

return await CrawlCommand.Create().Parse(args).InvokeAsync();
