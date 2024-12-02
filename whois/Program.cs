using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using MySql.Data.MySqlClient;
using System.IO;
using System.Net;
using System.Threading;
using System.Diagnostics;

namespace whois
{
    #region Program Class starts:
    public class Program
    {
        // Connection string to connect to your MySQL database
        private static readonly string connectionString = "Server = localhost; Database = mydb; Uid = root; Pwd = L3tM31n ;";

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Starting Server");
                RunServer();
            }
            else
            {
                foreach (string command in args)
                {
                    ProcessCommand(command);
                }
            }
        }

        #region Static client section:
        static void ProcessCommand(string command)
        {
            Console.WriteLine($"\nCommand: {command}\n");

            string[] parts = command.Split('?', 2);
            string loginID = parts[0];
            string? operation = parts.Length > 1 ? parts[1] : null;

            string? userID = Convert_Loginid_into_UserID(loginID);
            if (userID == null)
            {
                Console.WriteLine($"operation failed.");
                return;
            }

            if (operation == null)
            {
                Dump(userID); // showing user's information
            }
            else
            {
                string[] operationParts = operation.Split('=', 2);
                string field = operationParts[0];
                string? update = operationParts.Length > 1 ? operationParts[1] : null;

                if (update == null)
                {
                    Lookup(userID, field); // showing user's current location
                }
                else
                {
                    Update(userID, field, update); // update user's location 
                }
            }
        }


        // Map Login ID to User ID
        static string? Convert_Loginid_into_UserID(string loginID)
        {
            using var connection = new MySqlConnection(connectionString);
            connection.Open();

            string query = "SELECT `User ID` FROM `Login ID Table` WHERE `Login ID` = @LoginID";
            using var cmd = new MySqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@LoginID", loginID);

            object result = cmd.ExecuteScalar();

            if (result == null)
            {
                Console.WriteLine($"Login ID '{loginID}' not found in database.");
                return null;
            }

            return result?.ToString();
        }


        static void Dump(string userID)
        {
            using var connection = new MySqlConnection(connectionString);
            connection.Open();

            // Query to fetch data from the Users table
            string userQuery = "SELECT `User ID`, `Surname`, `Forenames`, `Title`, `Position`, `Location` FROM `Users` WHERE `User ID` = @UserID";
            using var userCmd = new MySqlCommand(userQuery, connection);
            userCmd.Parameters.AddWithValue("@UserID", userID);

            using var userReader = userCmd.ExecuteReader();

            // printing details
            if (userReader.Read()) 
            {
                Console.WriteLine($"UserID = {userReader["User ID"]}");
                Console.WriteLine($"Surname = {userReader["Surname"]}");
                Console.WriteLine($"Forenames = {userReader["Forenames"]}");
                Console.WriteLine($"Title = {userReader["Title"]}");
                Console.WriteLine($"Position = {userReader["Position"]}");
                Console.WriteLine($"Location = {userReader["Location"]}");
            }
            else
            {
                Console.WriteLine($"No user found with User ID '{userID}'.");
                return;
            }
            userReader.Close();

            // Query to fetch the phone
            string phoneQuery = "SELECT `Phone` FROM `phone` WHERE `User ID` = @UserID";
            using var phoneCmd = new MySqlCommand(phoneQuery, connection);
            phoneCmd.Parameters.AddWithValue("@UserID", userID);

            object phoneResult = phoneCmd.ExecuteScalar();
            if (phoneResult != null)
            {
                Console.WriteLine($"Phone = {phoneResult}");
            }
            else
            {
                Console.WriteLine("Phone = Not Found");
            }

            // Query to fetch the email
            string emailQuery = "SELECT `Email` FROM `email table` WHERE `User ID` = @UserID";
            using var emailCmd = new MySqlCommand(emailQuery, connection);
            emailCmd.Parameters.AddWithValue("@UserID", userID);

            object emailResult = emailCmd.ExecuteScalar();
            if (emailResult != null)
            {
                Console.WriteLine($"Email = {emailResult}");
            }
            else
            {
                Console.WriteLine("Email = Not Found");
            }
        }

        // For fetching the location
        static void Lookup(string userID, string field)
        {
            using var connection = new MySqlConnection(connectionString);
            connection.Open();

            string query = $"SELECT `{field}` FROM `Users` WHERE `User ID` = @UserID";
            using var cmd = new MySqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@UserID", userID);

            object result = cmd.ExecuteScalar();
            if (result != null)
            {
                Console.WriteLine($"{field}={result}");
            }
            else
            {
                Console.WriteLine($"Field '{field}' not found for User ID '{userID}'.");
            }
        }

        // For updating the location
        static void Update(string userID, string field, string update)
        {
            using var connection = new MySqlConnection(connectionString);
            connection.Open();

            string query = $"UPDATE `Users` SET `{field}` = @UpdateValue WHERE `User ID` = @UserID";
            using var cmd = new MySqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@UpdateValue", update);
            cmd.Parameters.AddWithValue("@UserID", userID);

            int rowsAffected = cmd.ExecuteNonQuery();
            Console.WriteLine(rowsAffected > 0 ? "OK" : "Update failed");
        }
        #endregion


        #region Server section:
        static void RunServer()
        {
            const int port = 443; // port number
            TcpListener server = new TcpListener(IPAddress.Any, port);

            try
            {
                server.Start();
                Console.WriteLine($"Server listening on port {port}");

                while (true)
                {
                    // Accept client connections
                    TcpClient client = server.AcceptTcpClient();
                    Console.WriteLine("Client connected.");

                    // Handle the client request in a new thread
                    ThreadPool.QueueUserWorkItem(HandleClient, client);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
            finally
            {
                server.Stop();
                Console.WriteLine("Server stopped.");
            }

            
            // Handles incoming client requests over a TCP connection, it reads the request, processes Get
            // and Post methods. Apart from these 2 jobs, it also has the Timeout handling
            // and error handling section

            static void HandleClient(object? obj)
            {
                TcpClient client = (TcpClient)obj!;

                try
                {
                    using var stream = client.GetStream();

                    stream.ReadTimeout = 1000;

                    using var reader = new StreamReader(stream, Encoding.UTF8);
                    using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
                    string? requestLine = null;

                    // Read the HTTP request
                    try
                    {
                        requestLine = reader.ReadLine();
                        if (string.IsNullOrWhiteSpace(requestLine)) return;
                    }
                    catch (IOException)
                    {
                        // If the timeout is hit, log and return (closing connection)
                        Console.WriteLine("Client timed out waiting for data.");
                        return;
                    }

                    Console.WriteLine($"Request: {requestLine}");
                    string[] requestParts = requestLine.Split(' ');

                    // To check if the request has 3 parts: Get / Post, Path and HTTP version. 
                    // If its not formed in 3 parts then it writes a bad request
                    if (requestParts.Length < 3)
                    {
                        WriteBadRequest(writer);
                        return;
                    }

                    string method = requestParts[0]; // Extracts the HTTP method
                    string path = requestParts[1];   // Extracts the resource path or end point

                    if (method == "GET")
                    {
                        if (path.StartsWith("/?name=")) // Query string requests like /?name=cssbct
                        {
                            string name = Uri.UnescapeDataString(path.Substring(7)); // Extracts name after '?name='
                            Console.WriteLine($"Received GET for name: {name}");
                            HandleGetRequest(name, writer);
                        }
                        // Handle dynamic path-based requests like /cssbct
                        else if (path.Length > 1) // Path-based requests like /cssbct
                        {
                            if (path.StartsWith("/"))
                            {
                                string name = Uri.UnescapeDataString(path.TrimStart('/')); // Extract name after '/'
                                Console.WriteLine($"Received GET for dynamic path: {name}");
                                HandleGetRequest(name, writer);
                            }
                            else
                            {
                                WriteBadRequest(writer); // Path is not in the expected format
                            }
                        }
                        else
                        {
                            WriteBadRequest(writer); // Invalid GET request
                        }
                    }


                    else if (method == "POST" && path == "/")
                    {
                        string body = ReadRequestBody(reader);

                        // Parse body parameters
                        var parameters = ParseFormData(body);
                        if (!parameters.ContainsKey("name") || !parameters.ContainsKey("location"))
                        {
                            WriteBadRequest(writer);
                            return;
                        }

                        string name = parameters["name"];
                        string location = parameters["location"];
                        Console.WriteLine($"Received POST: name = {name}, location = {location}");

                        // Fetch the UserID from the LoginID (name)
                        string? userID = Retrieve_User_ID_From_Login_ID(name);
                        if (userID == null)
                        {
                            WriteNotFound(writer);
                            return;
                        }

                        // Call UpdateLocation to update the database
                        if (UpdateLocation(userID, location))
                        {
                            WriteOK(writer, $"Location updated successfully for {name} to {location}.");
                        }
                        else
                        {
                            WriteNotFound(writer); // Respond with 404 if update fails
                        }
                    }


                    else
                    {
                        WriteBadRequest(writer);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error handling client: {ex.Message}");
                }
                finally
                {
                    client.Close();
                    Console.WriteLine("Client disconnected.");
                }
            }

            static void HandleGetRequest(string loginID, StreamWriter writer)
            {
                string? userID = Retrieve_User_ID_From_Login_ID(loginID);

                if (userID == null)
                {
                    WriteNotFound(writer);
                    return;
                }

                string? location = GetLocationByUserID(userID);


                if (location == null)
                {
                    WriteNotFound(writer);
                }
                else
                {
                    WriteOK(writer, location);
                }
            }

            // Helper to parse form data into a dictionary
            static Dictionary<string, string> ParseFormData(string body)
            {
                var dictionary = new Dictionary<string, string>();
                var pairs = body.Split('&');
                foreach (var pair in pairs)
                {
                    var keyValue = pair.Split('=');
                    if (keyValue.Length == 2)
                    {
                        dictionary[Uri.UnescapeDataString(keyValue[0])] = Uri.UnescapeDataString(keyValue[1]);
                    }
                }
                return dictionary;
            }



            static string? Retrieve_User_ID_From_Login_ID(string loginID)
            {
                using var connection = new MySqlConnection(connectionString);
                connection.Open();

                string query = "SELECT `User ID` FROM `Login ID Table` WHERE `Login ID` = @LoginID";
                using var cmd = new MySqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@LoginID", loginID);   // Safely injects the value of login id into the query
                object? result = cmd.ExecuteScalar();               //Executes the query and retrieves the first result.
                return result?.ToString();
            }

            static string? GetLocationByUserID(string userID)
            {
                using var connection = new MySqlConnection(connectionString);
                connection.Open();

                string query = "SELECT `Location` FROM `Users` WHERE `User ID` = @UserID";
                using var cmd = new MySqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@UserID", userID);

                return cmd.ExecuteScalar()?.ToString();
            }

            static bool UpdateLocation(string userID, string newLocation)
            {
                using var connection = new MySqlConnection(connectionString);
                connection.Open();

                string query = "UPDATE `Users` SET `Location` = @Location WHERE `User ID` = @UserID";
                using var cmd = new MySqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@Location", newLocation);
                cmd.Parameters.AddWithValue("@UserID", userID);

                int rowsAffected = cmd.ExecuteNonQuery();

                if (rowsAffected > 0)
                {
                    Console.WriteLine($"Location updated for UserID {userID} to {newLocation}.");
                    return true;
                }
                else
                {
                    Console.WriteLine($"Failed to update location for UserID {userID}. No rows affected.");
                    return false;
                }
            }



            static void WriteOK(StreamWriter writer, string responseBody = "OK")
            {
                var stopwatch = Stopwatch.StartNew();

                try
                {
                    writer.WriteLine("HTTP/1.1 200 OK");
                    writer.WriteLine("Content-Type: text/plain");
                    writer.WriteLine($"Content-Length: {responseBody.Length}");
                    writer.WriteLine("Connection: close"); // Explicitly state the connection will close
                    writer.WriteLine(); // End of headers
                    writer.WriteLine(responseBody); // Send response body
                    writer.Flush(); // Ensure data is sent before closing
                }
                catch (IOException)
                {
                    Console.WriteLine("Client timed out waiting for response.");
                    return;
                }
                finally
                {
                    stopwatch.Stop();
                    if (stopwatch.ElapsedMilliseconds > 1000)
                    {
                        Console.WriteLine("Server timeout: Took longer than 1 second to send the response.");
                    }
                }
            }

            static void WriteNotFound(StreamWriter writer)
            {
                writer.WriteLine("HTTP/1.1 400 Not Found");
                writer.WriteLine("Content-Type: text/plain");
                writer.WriteLine("Connection: close");
                writer.WriteLine();
                writer.WriteLine("The requested resource could not be found.");
            }

            static void WriteBadRequest(StreamWriter writer)
            {
                writer.WriteLine("HTTP/1.1 400 Bad Request");
                writer.WriteLine("Content-Type: text/plain");
                writer.WriteLine("Connection: close");
                writer.WriteLine();
                writer.WriteLine("Invalid request.");
            }


            // Read the body from an HTTP request from a stream Reader 
            // Designed to tackle requests with Content-length header
            static string ReadRequestBody(StreamReader reader)
            {
                string line;
                int contentLength = 0;

                // Read headers
                while (!string.IsNullOrWhiteSpace(line = reader.ReadLine()!))
                {
                    if (line.StartsWith("Content-Length: "))
                    {
                        contentLength = int.Parse(line.Substring(16));
                    }
                }

                if (contentLength == 0) return string.Empty;

                // Read body
                char[] body = new char[contentLength];
                reader.Read(body, 0, contentLength);
                return new string(body);
            }
        }
        #endregion
    }
    #endregion
}