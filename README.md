# Butterfly.Message ![Butterfly Logo](https://raw.githubusercontent.com/firesharkstudios/Butterfly/master/img/logo-40x40.png) 

> Send emails and text messages via the same API in C#

# Install from Nuget

| Name | Package | Install |
| --- | --- | --- |
| Butterfly.Message | [![nuget](https://img.shields.io/nuget/v/Butterfly.Message.svg)](https://www.nuget.org/packages/Butterfly.Message/) | `nuget install Butterfly.Message` |
| Butterfly.Message.Aws | [![nuget](https://img.shields.io/nuget/v/Butterfly.Message.Aws.svg)](https://www.nuget.org/packages/Butterfly.Message.Aws/) | `nuget install Butterfly.Message.Aws` |
| Butterfly.Message.Twilio | [![nuget](https://img.shields.io/nuget/v/Butterfly.Message.Twilio.svg)](https://www.nuget.org/packages/Butterfly.Message.Twilio/) | `nuget install Butterfly.Message.Twilio` |

# Install from Source Code

```git clone https://github.com/firesharkstudios/butterfly-message```

# Overview

```cs
// Parse messages
var sendMessageFileParser = new SendMessageFileParser();
var welcomeEmailSendMessage = sendMessageFileParser.Parse("welcome-email.liquid");
var welcomeTextSendMessage = sendMessageFileParser.Parse("welcome-text.liquid");

// Setup notify manager
var sendMessageQueueManager = new SendMessageQueueManager(
    database,
    emailMessageSender: new AwsSesEmailMessageSender(options.AwsAccessKeyId, options.AwsSecretAccessKey),
    textMessageSender: new TwilioTextMessageSender(options.TwilioAccountSid, options.TwilioAuthToken)
);

// Send welcome email
var welcomeEmail = welcomeEmailSendMessage.Evaluate(new Dict {
    ["first_name"] = "Kent",
    ["to"] = "kent@fireshark.com",
});
sendMessageQueueManager.Queue(welcomeEmail);

// Send welcome text
var welcomeText = welcomeTextSendMessage.Evaluate(new Dict {
    ["first_name"] = "Kent",
    ["to"] = "+13165551212",
});
sendMessageQueueManager.Queue(welcomeText);
```

Example of the ```welcome-email.liquid``` file...
```
From: Kent Johnson <kent@fireshark.com>
To: {{ first_name }} <{{ to }}>
Subject: Welcome

Hello, {{ user.first_name }}.  To get started, do these things. 
```

Example of the ```welcome-text.liquid``` file...
```
From: +13165551212
To: {{ to }}

Hello, {{ user.first_name }}.  To get started, do these things. 
```

# Contributing

If you'd like to contribute, please fork the repository and use a feature
branch. Pull requests are warmly welcome.

# Licensing

The code is licensed under the [Mozilla Public License 2.0](http://mozilla.org/MPL/2.0/).  
