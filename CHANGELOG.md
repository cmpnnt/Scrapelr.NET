# Changelog

All notable changes to this project are documented in this file.

## [0.1.1] - 2026-10-10

### Bug Fixes

- Find the executable by its AssemblyName in tests and the release workflow ([2861982](https://github.com/cmpnnt/Scrapelr.NET/commit/28619820e9d41c39d4f99cacfd4d439bc7f8c822))

### Build

- **Scrapelr.NET:** Shorten name ([57d0193](https://github.com/cmpnnt/Scrapelr.NET/commit/57d0193d14ddc6671ad05230c848a839b051dab5))

## [0.1.0] - 2026-10-10

### Features

- Add link scraping logic ([4780698](https://github.com/cmpnnt/Scrapelr.NET/commit/4780698d2b56e2f8f622ca678e219544670a3d4d))
- Add Task runner to process scraped URLs from channel ([e9cb499](https://github.com/cmpnnt/Scrapelr.NET/commit/e9cb4993dd5a02b9fe7170647a3bf842909a9c02))
- Implemented link depth tracking ([be52fb7](https://github.com/cmpnnt/Scrapelr.NET/commit/be52fb72317b1fca6ef50dab7559051563b1deca))
- Merge crawled pages into a single PDF and add CLI options including --outputFile ([7b9fb98](https://github.com/cmpnnt/Scrapelr.NET/commit/7b9fb9814608abfd29c367ad8b2afac058d40bf9))
- Add --inputFile for crawling from a list of URLs. Inject collector and processor factories into Pipeline ([ae0d13a](https://github.com/cmpnnt/Scrapelr.NET/commit/ae0d13aa1d0939f97736f8f221fec9afa016f5dc))
- Add Markdown output as an alternative to PDF ([03a9a91](https://github.com/cmpnnt/Scrapelr.NET/commit/03a9a917a582ddecbdc010285d3c6147acaabb20))

### Bug Fixes

- Stop republishing seen links, filter against the starting URL and honour MaxDepth exactly ([ca3a23f](https://github.com/cmpnnt/Scrapelr.NET/commit/ca3a23fc1ed381880de7c9479dd63d89249e6665))
- Ignore non-http links such as mailto: and javascript: ([9c11e7d](https://github.com/cmpnnt/Scrapelr.NET/commit/9c11e7d2cc67c879a66ae3f757d363008cf899e9))
- Exclude the test project from solution-wide publish ([0da33b8](https://github.com/cmpnnt/Scrapelr.NET/commit/0da33b8e0eb33c6c13082610ccb9e32814764ccf))

### Refactoring

- Move collector logic from Program.cs to Collector.cs ([a381253](https://github.com/cmpnnt/Scrapelr.NET/commit/a38125358d20155a6d5d14f9ebd0a416e4665bb0))
- Extract BrowserFactory, CrawlOptions and ICollector ([91b1cdb](https://github.com/cmpnnt/Scrapelr.NET/commit/91b1cdbc027d4b88b3062bcdb5e4db6c631b5c97))
- Replace Spectre.Console.Cli with System.CommandLine for native AOT support ([8230803](https://github.com/cmpnnt/Scrapelr.NET/commit/82308036703a80de571209501b2fb2632983cdcd))

### Documentation

- Update comments ([c1a1852](https://github.com/cmpnnt/Scrapelr.NET/commit/c1a1852bdda60c9a83dc07599440fc70dc7abc22))

### Tests

- Add TUnit unit tests for crawling, rendering, merging and CLI parsing ([01e2b1e](https://github.com/cmpnnt/Scrapelr.NET/commit/01e2b1ec1d4e73d1fa557c85f4d8f5b4d9bb868b))
- Add end-to-end tests that crawl a local site with a real browser ([cacc235](https://github.com/cmpnnt/Scrapelr.NET/commit/cacc2358fbf01926720f76e977f33bb5f88521a4))

### Build

- **Scrapelr.NET:** Reducing file size ([603f3b2](https://github.com/cmpnnt/Scrapelr.NET/commit/603f3b2c92fa471a259cabfd448c45956d8c0c78))

### Continuous Integration

- Add release workflow with a Conventional Commits changelog ([b29be93](https://github.com/cmpnnt/Scrapelr.NET/commit/b29be93919387f1337d90feb4c33fe352108ea8d))

### Miscellaneous

- Add MPL license ([ef55f75](https://github.com/cmpnnt/Scrapelr.NET/commit/ef55f759f7f7c9542db975ec5078071506257c32))


