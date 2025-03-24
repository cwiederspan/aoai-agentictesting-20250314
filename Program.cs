// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using dotenv.net;
using Azure.Identity;
using Azure.AI.Projects;

namespace AgenticTesting;

public class Program {

    static async Task Main() {

        DotEnv.Load();

        var connectionString = Environment.GetEnvironmentVariable("PROJECT_CONNECTION_STRING");
        var client = new AgentsClient(connectionString, new DefaultAzureCredential());

        // Step 1: Create an agent
        var agentResponse = await client.CreateAgentAsync(
            model: "gpt-4o-mini",
            name: "My Agent",
            instructions: "You are a helpful agent.",
            tools: new List<ToolDefinition> { new CodeInterpreterToolDefinition() });

        var agent = agentResponse.Value;

        // Intermission: agent should now be listed

        var agentListResponse = await client.GetAgentsAsync();

        //// Step 2: Create a thread
        var threadResponse = await client.CreateThreadAsync();
        var thread = threadResponse.Value;

        // Step 3: Add a message to a thread
        var messageResponse = await client.CreateMessageAsync(
            thread.Id,
            MessageRole.User,
            "I need to solve the equation `3x + 11 = 14`. Can you help me?");

        var message = messageResponse.Value;

        // Intermission: message is now correlated with thread
        // Intermission: listing messages will retrieve the message just added

        var messagesListResponse = await client.GetMessagesAsync(thread.Id);
        //Assert.That(messagesListResponse.Value.Data[0].Id == message.Id);

        // Step 4: Run the agent
        var runResponse = await client.CreateRunAsync(
            thread.Id,
            agent.Id,
            additionalInstructions: "");

        var run = runResponse.Value;

        do {
            await Task.Delay(TimeSpan.FromMilliseconds(500));
            runResponse = await client.GetRunAsync(thread.Id, runResponse.Value.Id);
        } while (runResponse.Value.Status == RunStatus.Queued || runResponse.Value.Status == RunStatus.InProgress);

        var afterRunMessagesResponse = await client.GetMessagesAsync(thread.Id);
        var messages = afterRunMessagesResponse.Value.Data;

        // Note: messages iterate from newest to oldest, with the messages[0] being the most recent
        foreach (var threadMessage in messages) {

            Console.Write($"{threadMessage.CreatedAt:yyyy-MM-dd HH:mm:ss} - {threadMessage.Role,10}: ");

            foreach (var contentItem in threadMessage.ContentItems) {
                
                if (contentItem is MessageTextContent textItem) {
                    Console.Write(textItem.Text);
                }
                else if (contentItem is MessageImageFileContent imageFileItem) {
                    Console.Write($"<image from ID: {imageFileItem.FileId}");
                }

                Console.WriteLine();
            }
        }
    }
}