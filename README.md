Extracts Zelle transaction information from the Chase Bank website.

DISCLAIMER: No guarantees are made. Use at your own risk.

Currently only implements one feature: generating a CSV file from the list of received payments. Any future redesign of the Chase website will break this feature.

Instructions:
- Build the .NET solution from this project's source code. You should end up with a file called `ChaseZelleReader.exe`.
- Log in to chase.com using the Chrome browser.
- Click "Pay & Transfer" -> "Payment Activity" -> "Money Received". You should see the list of received payments.
- Right-click on an empty part of the page and choose "Inspect". You should see the Elements tab.
- Right-click the root `<html` element and choose "Copy" => "Copy Element".
- Paste into a text editor and save the file.
- Run the file using `ChaseZelleReader.exe`. In Windows, you can do this by dragging the saved text file onto the exe.
- A .csv file will be generated containing the details of the received transactions.
