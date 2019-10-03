# SyncMLViewer

A small real time SyncML protocol Viewer. Helping during troubleshooting to see what is the client actually receiving. Verifying OMA-URIs and data field definitions. 

Inspired by the blog post from Michael Niehaus about real time troubleshooting with Message Analyzer, I thought it might be handy to have a simple tool dedicated for that purpose to watch the SyncML protocol in real time.

Very helpful resources I used during implementation:  

Inspired by Michael Niehaus - @mniehaus - blog about monitoring realtime MDM activity  
https://oofhours.com/2019/07/25/want-to-watch-the-mdm-client-activity-in-real-time/

[MS-MDM]: Mobile Device Management Protocol  
https://docs.microsoft.com/en-us/openspecs/windows_protocols/ms-mdm/

OMA DM protocol support - Get all the details how it is working...  
https://docs.microsoft.com/en-us/windows/client-management/mdm/oma-dm-protocol-support

SyncML response status codes  
https://docs.microsoft.com/en-us/windows/client-management/mdm/oma-dm-protocol-support#syncml-response-codes

Thanks to Matt Graeber - @mattifestation - for the extended ETW Provider list  
https://gist.github.com/mattifestation/04e8299d8bc97ef825affe733310f7bd/
https://gist.githubusercontent.com/mattifestation/04e8299d8bc97ef825affe733310f7bd/raw/857bfbb31d0e12a8ebc48a95f95d298222bae1f6/NiftyETWProviders.json

more MDM ETW Provider details  
https://docs.microsoft.com/en-us/windows/client-management/mdm/diagnose-mdm-failures-in-windows-10
