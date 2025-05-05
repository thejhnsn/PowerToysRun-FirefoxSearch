# PowerToysRun-FirefoxSearch
A plugin for Powertoys Run that searches your Firefox bookmarks and history!

## Installation

1. Download the latest release from the [releases page](https://github.com/thejhnsn/PowerToysRun-FirefoxSearch/releases)
2. Extract the contents of the zip file to your PowerToys Run plugins directory. This is usually `%LOCALAPPDATA%\Microsoft\PowerToys\PowerToys Run\Plugins`

## Usage

1. Open PowerToys Run (by default `Alt+Space`).
2. Type `ff` to search your Firefox bookmarks and history.
3. Select a result to open it in your default browser. Alternatively, copy the title or URL to the clipboard.

## Build Instructions

1. Clone the repository.
2. Build the project by using the included `build.ps1` script or by running `dotnet build` from the command line.

## Configuration
You can configure the plugin by going into the PowerToys settings and selecting the "PowerToys Run" tab. From there, you can:
- Enable or disable the plugin
- Change the search prefix (default is `ff`)
- Change whether to search bookmarks, history, or both (default is both)
- Change the maximum number of results to display (default is 40)

## Contributing and Issues

Contributions are welcome! Please fork the repository and submit a pull request. If you find a bug or have a feature request, please open an issue.

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.