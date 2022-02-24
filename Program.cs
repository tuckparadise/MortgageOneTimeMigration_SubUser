using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Xml;
//using Microsoft.VisualBasic.IO;
using System.Data;
using System.Data.SqlClient;
using System.Web;
using System.Diagnostics;

namespace MortgageOneTimeMigration_DevCode
{
    class Program
    {
        private static HttpWebRequest CreateWebRequest(string url, string action)
        {
            HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(url);
            webRequest.Headers.Add("SOAPAction", action);
            webRequest.ContentType = "text/xml;charset=\"utf-8\"";
            webRequest.Accept = "text/xml";
            webRequest.Method = "POST";
            return webRequest;
        }

        private static XmlDocument CreateSoapEnvelope()
        {
            XmlDocument soapEnvelopeDocument = new XmlDocument();
            /*
            soapEnvelopeDocument.LoadXml(
            @"<SOAP-ENV:Envelope xmlns:SOAP-ENV=""http://schemas.xmlsoap.org/soap/envelope/"" 
               xmlns:xsi=""http://www.w3.org/1999/XMLSchema-instance"" 
               xmlns:xsd=""http://www.w3.org/1999/XMLSchema"">
        <SOAP-ENV:Body>
            <DP_GetEncryptionKey xmlns=""http://tempuri.org/"" 
                SOAP-ENV:encodingStyle=""http://schemas.xmlsoap.org/soap/encoding/"">                
            </DP_GetEncryptionKey>
        </SOAP-ENV:Body>
    </SOAP-ENV:Envelope>");
            */
            soapEnvelopeDocument.LoadXml(
          @"<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
              <soap:Body>
                <DP_GetEncryptionKey xmlns=""http://tempuri.org/"">
                  <EncryptionKey>string</EncryptionKey>
                  <error>string</error>
                </DP_GetEncryptionKey>
              </soap:Body>
            </soap:Envelope>");
            return soapEnvelopeDocument;
        }

        private static XmlDocument CreateSoapEnvelope_EncryptText(string text, string key)
        {
            XmlDocument soapEnvelopeDocument = new XmlDocument();

            string strEnvelope = @"<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
                <soap:Body><EncryptText xmlns=""http://tempuri.org/"">";
            strEnvelope = strEnvelope + "<plainText>" + text + "</plainText>";
            strEnvelope = strEnvelope + "<passPhrase>" + key + "</passPhrase>";
            strEnvelope = strEnvelope + "</EncryptText></soap:Body></soap:Envelope>";

            soapEnvelopeDocument.LoadXml(strEnvelope);
            return soapEnvelopeDocument;
        }

        private static XmlDocument CreateSoapEnvelope_DecryptText(string text, string key)
        {
            XmlDocument soapEnvelopeDocument = new XmlDocument();

            string strEnvelope = @"<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
                <soap:Body><DecryptText xmlns=""http://tempuri.org/"">";
            strEnvelope = strEnvelope + "<cipherText>" + text + "</cipherText>";
            strEnvelope = strEnvelope + "<passPhrase>" + key + "</passPhrase>";
            strEnvelope = strEnvelope + "</DecryptText></soap:Body></soap:Envelope>";

            soapEnvelopeDocument.LoadXml(strEnvelope);
            return soapEnvelopeDocument;
        }

        private static void InsertSoapEnvelopeIntoWebRequest(XmlDocument soapEnvelopeXml, HttpWebRequest webRequest)
        {
            using (Stream stream = webRequest.GetRequestStream())
            {
                soapEnvelopeXml.Save(stream);
            }
        }

        private static string DecryptTextFromTTMSWindowsEncryptionTool(string EncryptedText)
        {
            string ExePath;
            string CurrentPath = Directory.GetCurrentDirectory();
            //string CurrentPath = Server.MapPath(".");
            string result = "";
            Process compiler = new Process();
            ExePath = CurrentPath + "\\TTMSEncryptText.exe";
            compiler.StartInfo.FileName = ExePath;
            compiler.StartInfo.Arguments = "decrypt " + EncryptedText;
            compiler.StartInfo.UseShellExecute = false;
            compiler.StartInfo.RedirectStandardError = true;
            compiler.StartInfo.RedirectStandardOutput = true;
            compiler.Start();

            string ExeOutput = compiler.StandardOutput.ReadToEnd();

            compiler.WaitForExit();

            if (compiler.ExitCode == 0)
            {
                result = ExeOutput;
            }
            return result;
        }

        static void Main(string[] args)
        {
            try
            {
                var directory = Directory.GetCurrentDirectory();

                var path = directory + @"\WS_URL.txt";
                var DBConfigpath = directory + @"\DB_Config.txt";

                var URL = "";
                var DBServer = "";
                var DBName = "";
                var DBUserName = "";
                var DBUserPassword = "";

                using (var reader = new StreamReader(path))
                {
                    while (!reader.EndOfStream)
                    {
                        var line = reader.ReadLine();
                        URL = line;
                    }
                }

                using (var reader = new StreamReader(DBConfigpath))
                {
                    //while (!reader.EndOfStream)
                    //{
                    var line = reader.ReadLine();
                    DBServer = line;
                    line = reader.ReadLine();
                    DBName = line;
                    line = reader.ReadLine();
                    DBUserName = line;
                    line = reader.ReadLine();
                    DBUserPassword = line;
                    //}
                }

                var _url = URL;

                // get key 
                var _action = "http://tempuri.org/DP_GetEncryptionKey";

                XmlDocument soapEnvelopeXml = CreateSoapEnvelope();
                HttpWebRequest webRequest = CreateWebRequest(_url, _action);
                InsertSoapEnvelopeIntoWebRequest(soapEnvelopeXml, webRequest);

                // begin async call to web request.
                IAsyncResult asyncResult = webRequest.BeginGetResponse(null, null);

                // suspend this thread until call is complete. You might want to
                // do something usefull here like update your UI.
                asyncResult.AsyncWaitHandle.WaitOne();

                // get the response from the completed web request.
                string soapResult;
                string key = "";

                using (WebResponse webResponse = webRequest.EndGetResponse(asyncResult))
                {
                    using (StreamReader rd = new StreamReader(webResponse.GetResponseStream()))
                    {
                        soapResult = rd.ReadToEnd();
                        XmlDocument doc = new XmlDocument();
                        doc.LoadXml(soapResult);

                        XmlNodeList elemList = doc.GetElementsByTagName("EncryptionKey");
                        key = elemList[0].InnerXml.ToString();

                    }
                }

                // get password   
                /*
                var _action_getdecryptedpw = "http://tempuri.org/DecryptText";
                XmlDocument soapEnvelopeXml_getdecryptedpw = CreateSoapEnvelope_DecryptText(DBUserPassword, key);

                HttpWebRequest webRequest_getdecryptedpw = CreateWebRequest(_url, _action_getdecryptedpw);
                InsertSoapEnvelopeIntoWebRequest(soapEnvelopeXml_getdecryptedpw, webRequest_getdecryptedpw);

                // begin async call to web request.
                IAsyncResult asyncResult_getdecryptedpw = webRequest_getdecryptedpw.BeginGetResponse(null, null);

                // suspend this thread until call is complete. You might want to
                // do something usefull here like update your UI.
                asyncResult_getdecryptedpw.AsyncWaitHandle.WaitOne();

                // get the response from the completed web request.
                //string soapResult;
                //string key = "";
                string decrypted_DbPW = "";

                using (WebResponse webResponse = webRequest_getdecryptedpw.EndGetResponse(asyncResult_getdecryptedpw))
                {
                    using (StreamReader rd = new StreamReader(webResponse.GetResponseStream()))
                    {
                        soapResult = rd.ReadToEnd();
                        XmlDocument doc = new XmlDocument();
                        doc.LoadXml(soapResult);

                        XmlNodeList elemList = doc.GetElementsByTagName("DecryptTextResult");
                        decrypted_DbPW = elemList[0].InnerXml.ToString();

                    }
                }
                */
                string decrypted_DbPW = DecryptTextFromTTMSWindowsEncryptionTool(DBUserPassword);

                var csvpath = directory + @"\data.csv";



                //var path = @"C:\Person.csv"; // Habeeb, "Dubai Media City, Dubai"
                /*
                using (TextFieldParser csvParser = new TextFieldParser(csvpath))
                {
                    csvParser.CommentTokens = new string[] { "#" };
                    csvParser.SetDelimiters(new string[] { "," });
                    csvParser.HasFieldsEnclosedInQuotes = true;

                    // Skip the row with the column names
                    csvParser.ReadLine();

                    while (!csvParser.EndOfData)
                    {
                        // Read current line fields, pointer moves to the next line.
                        string[] fields = csvParser.ReadFields();
                        string Name = fields[0];
                        string Address = fields[1];
                    }
                }
                */
                using (CsvTextFieldParser csvParser = new CsvTextFieldParser(csvpath))

                //using (TextFieldParser csvParser = new TextFieldParser(csvpath))
                //using (var reader = new StreamReader(csvpath))
                {
                    //List<string> projectcode = new List<string>();
                    //List<string> projectname = new List<string>();
                    //List<string> developercode = new List<string>();

                    //csvParser.CommentTokens = new string[] { "#" };
                    //csvParser.SetDelimiters(new string[] { "," });
                    csvParser.HasFieldsEnclosedInQuotes = true;
                    
                    // Skip the row with the column names
                    //csvParser.ReadLine();
                    csvParser.ReadFields();
                    //reader.ReadLine();

                    int index = 1;

                    //while (!reader.EndOfStream)
                    while (!csvParser.EndOfData)
                    {
                        /*
                        var line = reader.ReadLine();
                        var values = line.Split(',');

                        string id = values[0];
                        string name = values[1];                        
                        string email = values[2];

                        listID.Add(values[0]);
                        listName.Add(values[1]);
                        listEmail.Add(values[2]);
                        */

                        string[] fields = csvParser.ReadFields();
                        //string id = fields[0];
                        //string name = fields[1];
                        //string email = fields[2];
                        string id = fields[0];
                        string name = fields[1];
                        string email = fields[2];                        
                        string developercode = fields[3];
                        string runningid = fields[4];

                        // _action = "http://tempuri.org/EncryptText";
                        // XmlDocument soapEnvelopeXml2 = CreateSoapEnvelope_EncryptText(password, key);
                        //HttpWebRequest webRequest2 = CreateWebRequest(_url, _action);
                        //InsertSoapEnvelopeIntoWebRequest(soapEnvelopeXml2, webRequest2);

                        // begin async call to web request.
                        //IAsyncResult asyncResult2 = webRequest2.BeginGetResponse(null, null);

                        //asyncResult2.AsyncWaitHandle.WaitOne();

                        //string EncryptedPassword = "";

                        // using (WebResponse webResponse2 = webRequest2.EndGetResponse(asyncResult2))
                        // {
                        // using (StreamReader rd = new StreamReader(webResponse2.GetResponseStream()))
                        // {
                        //soapResult = rd.ReadToEnd();

                        // XmlDocument doc = new XmlDocument();
                        // doc.LoadXml(soapResult);

                        //  XmlNodeList elemList = doc.GetElementsByTagName("EncryptTextResult");
                        // EncryptedPassword = elemList[0].InnerXml.ToString();

                        // get default password 
                        string connstr = @"Data Source=" + DBServer + ";Initial Catalog=" + DBName + ";Persist Security Info=True;User ID=" + DBUserName + ";Password=" + decrypted_DbPW;

                        SqlConnection conn = null;
                        SqlDataAdapter sqlDA = null;
                        conn = new SqlConnection(connstr);

                        sqlDA = new SqlDataAdapter();
                        //sqlDA.SelectCommand = new SqlCommand("insert into [SQLSolicitor] values ('" + id + "','" + EncryptedPassword + "','Active','" + name + "',getdate(),getdate(),'system','system',NULL,'Yes',getdate(),getdate(),NULL)", conn);
                        sqlDA.SelectCommand = new SqlCommand("dbo.ddMaintenance_SQLDeveloper_PasswordReset_updatePassword @id, @defaultPassword output", conn);

                        sqlDA.SelectCommand.Parameters.AddWithValue("@id", "");

                        sqlDA.SelectCommand.Parameters.Add("@defaultPassword", SqlDbType.NVarChar, 4000);
                        sqlDA.SelectCommand.Parameters["@defaultPassword"].Direction = ParameterDirection.Output;

                        DataSet ds = new DataSet("ds");
                        sqlDA.Fill(ds);

                        string defaultPasswordDecrypted = sqlDA.SelectCommand.Parameters["@defaultPassword"].Value.ToString();

                        // encrypt text 
                        _action = "http://tempuri.org/EncryptText";
                        XmlDocument soapEnvelopeXml2 = CreateSoapEnvelope_EncryptText(defaultPasswordDecrypted, key);
                        HttpWebRequest webRequest2 = CreateWebRequest(_url, _action);
                        InsertSoapEnvelopeIntoWebRequest(soapEnvelopeXml2, webRequest2);

                        // begin async call to web request.
                        IAsyncResult asyncResult2 = webRequest2.BeginGetResponse(null, null);

                        asyncResult2.AsyncWaitHandle.WaitOne();

                        string EncryptedPassword = "";

                        using (WebResponse webResponse2 = webRequest2.EndGetResponse(asyncResult2))
                        {
                            using (StreamReader rd = new StreamReader(webResponse2.GetResponseStream()))
                            {
                                soapResult = rd.ReadToEnd();

                                XmlDocument doc = new XmlDocument();
                                doc.LoadXml(soapResult);

                                XmlNodeList elemList = doc.GetElementsByTagName("EncryptTextResult");
                                EncryptedPassword = elemList[0].InnerXml.ToString();
                            }
                        }
                        // insert 
                        //string connstr = @"Data Source=" + DBServer + ";Initial Catalog=" + DBName + ";Persist Security Info=True;User ID=" + DBUserName + ";Password=" + decrypted_DbPW;

                        //SqlConnection conn = null;
                        //SqlDataAdapter sqlDA = null;
                        conn = new SqlConnection(connstr);

                        sqlDA = new SqlDataAdapter();
                        //sqlDA.SelectCommand = new SqlCommand("insert into [SQLSolicitor] values ('" + id + "','" + EncryptedPassword + "','Active','" + name + "',getdate(),getdate(),'system','system',NULL,'Yes',getdate(),getdate(),NULL)", conn);
                        //sqlDA.SelectCommand = new SqlCommand("insert into [ddProjectDeveloper] values ('" + id + "','" + EncryptedPassword + "','Active','" + name + "',getdate(),getdate(),'system','system',NULL,'Yes',getdate(),getdate(),NULL)", conn);
                        //sqlDA.SelectCommand = new SqlCommand("insert into [ddProjectDeveloper] VALUES('" + id + "','" + EncryptedPassword + "','Active','" + name + "',getdate(),getdate(),'system','system',getdate(),'Yes',getdate(),getdate(),getdate(),null,'" + email + "',null,null,null,null,null)", conn);
                        //VALUES('" + id + "', '" + EncryptedPassword + "', 'Active', '" + name + "', getdate(), getdate(), 'system', 'system', null, 'Yes', getdate(), getdate(), getdate(), null, '" + email + "', null, null, null, null, null)
                        //sqlDA.SelectCommand = new SqlCommand("insert into [ddProjectDeveloperProjectMapping] VALUES('" + projectcode + "','" + projectname + "','" + developercode + "',getdate(),getdate(),'system','system')", conn);
                        sqlDA.SelectCommand = new SqlCommand("insert into [ddProjectDeveloperSubUser] ([ID],[Password],[Status],[Name],[CreatedDate],[UpdatedDate],[CreatedUser],[UpdatedUser],[LastLoginDatetime],[PasswordForceReset],[PasswordExpiryDate],[PasswordExpiryWarningDate],[IDLastLockedDatetime],[AcknowledgementFlag],[Email],[AcknowledgementDate],[DeveloperCode],[RunningID],[AcknowledgementUploaded],[AcknowledgementDateUploaded],[EmailSent],[EmailSentDate]) VALUES('" + id + "','" + EncryptedPassword + "','Active','" + name + "',getdate(),getdate(),'system','system',getdate(),1,null,null,null,null,'" + email + "',null,'" + developercode + "'," + runningid + ",null,null,null,null)", conn);

                        // DataSet ds = new DataSet("ds");
                        sqlDA.Fill(ds);

                        Console.WriteLine(index + ":" + id + "added\n");

                        index = index + 1;

                        //}
                        // }

                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;

            }
        }
    }
}
