using System;
using System.Threading;
using System.Threading.Tasks;
using Lime.Protocol;
using Takenet.MessagingHub.Client;
using Takenet.MessagingHub.Client.Listener;
using Takenet.MessagingHub.Client.Sender;
using System.Diagnostics;
using Takenet.Iris.Messaging.Contents;
using Takenet.MessagingHub.Client.Extensions.Contacts;
using Takenet.MessagingHub.Client.Extensions.Scheduler;
using Takenet.MessagingHub.Client.Extensions.Directory;
using Takenet.MessagingHub.Client.Extensions.Bucket;
using Lime.Messaging.Contents;
using System.Text.RegularExpressions;

namespace PillsReminder
{
    public class PlainTextMessageReceiver : IMessageReceiver
    {
        private readonly IMessagingHubSender _sender;
        private readonly IContactExtension _contacts;
        private readonly ISchedulerExtension _scheduler;
        private readonly IDirectoryExtension _directory; //para pegar os dados do cara sem ter que perguntar 
        private readonly IBucketExtension _bucket; //banco de dados local do bot

        private static string SState { get; set; }
        private static string Medicine { get; set; }

        public PlainTextMessageReceiver(IMessagingHubSender sender, IContactExtension contacts, ISchedulerExtension scheduler, IDirectoryExtension directory, IBucketExtension bucket)
        {
            _sender = sender;
            //_contacts = contacts;
            _scheduler = scheduler;
            _directory = directory;
            _bucket = bucket;
            SState = "initialState";
            Medicine = string.Empty;
        }

        public async Task ReceiveAsync(Message message, CancellationToken cancellationToken)
        {
            Trace.TraceInformation($"From: {message.From} \tContent: {message.Content}");

            var user = await _directory.GetDirectoryAccountAsync(message.From, cancellationToken);

            var msg = message.Content.ToString().ToLower();

            if (msg.Equals("ok") || msg.Equals("obrigada") || msg.Equals("blz") || msg.Equals("vlw") || msg.Equals("at� l�") || msg.Equals("at�") 
                || msg.Equals("tchau") || msg.Equals("flw") )
            {
                await _sender.SendMessageAsync(":)", message.From);
            }

            switch (SState)
            {
                case "initialState":
                    await _sender.SendMessageAsync(new Select
                    {
                        Text = $"Ol�, { user.FullName.Split(' ')[0] }! Bem-vindo(a) ao Pills Reminder. Eu vou lembrar voc� de tomar os seus medicamentos sempre no hor�rio! Est� pronto(a) para come�ar?",
                        Options = new[] {
                            new SelectOption {
                                Text = "Sim",
                                Value = new PlainText { Text = "Sim" }                                
                            },
                            new SelectOption
                            {
                                Text = "N�o",
                                Value = new PlainText { Text = "N�o" }
                            }
                        },
                        Scope = SelectScope.Immediate
                    },
                    message.From,
                    cancellationToken);
                    SState = "iniciado";
                    break;

                case "iniciado":
                    if (message.Content.ToString().Equals("N�o"))
                    {
                        SState = "cadastroPendente";
                        await _sender.SendMessageAsync("Tudo bem! Podemos fazer depois ent�o. Pode me chamar aqui quando quiser iniciar o cadastro. At� mais!", message.From);
                    }
                    else if(message.Content.ToString().Equals("Sim"))
                    {
                        SState = "cadastrarMedicamentos";
                        await _sender.SendMessageAsync("Oba! Ent�o vamos come�ar. Qual o nome do seu medicamento?", message.From);
                    } else
                    {
                        SState = "iniciado";
                        await _sender.SendMessageAsync(new Select
                        {
                            Text = "Desculpe, n�o entendi. Voc� quer iniciar o cadasatro dos seus medicamentos agora?",
                            Options = new[] {
                                new SelectOption {
                                    Text = "Sim",
                                    Value = new PlainText { Text = "Sim" }
                                },
                                new SelectOption
                                {
                                    Text = "N�o",
                                    Value = new PlainText { Text = "N�o" }
                                }
                            },
                            Scope = SelectScope.Immediate
                        },
                    message.From,
                    cancellationToken);
                    }
                    break;

                case "cadastrarMedicamentos":
                    Medicine = message.Content.ToString();
                    await _sender.SendMessageAsync("Anotado! E que horas voc� quer que eu te lembre de tom�-lo?", message.From);
                    SState = "agendarHorario";
                    break;

                case "cadastroPendente":
                    await _sender.SendMessageAsync(new Select
                    {
                        Text = $"Ol�, { user.FullName.Split(' ')[0] }! Bem-vindo(a) de volta. Est� pronto(a) para come�ar?",
                        Options = new[] {
                            new SelectOption {
                                Text = "Sim",
                                Value = new PlainText { Text = "Sim" }
                            },
                            new SelectOption
                            {
                                Text = "N�o",
                                Value = new PlainText { Text = "N�o" }
                            }
                        },
                        Scope = SelectScope.Immediate
                    },
                    message.From,
                    cancellationToken);
                    
                    SState = "iniciado";
                    break;

                case "agendarHorario":
                    if (!Regex.IsMatch(message.Content.ToString(), "^([0-9]|0[0-9]|1[0-9]|2[0-3]):[0-5][0-9]$"))
                    {
                        await _sender.SendMessageAsync("Opa! Esse n�o me parece um hor�rio v�lido. Escolha um hor�rio entre 00:00 e 23:59. Como, por exemplo, 12:00 ou 18:30.", message.From);
                        SState = "agendarHorario";
                    }
                    else
                    {
                        await _sender.SendMessageAsync("Combinado! Pode ficar tranquilo(a) que vou te avisar quando chegar a hora! At� l�!", message.From);
                        await _scheduler.ScheduleMessageAsync(new Message
                        {
                            Id = Guid.NewGuid().ToString(),
                            From = message.To,
                            Content = new PlainText { Text = $"Ol�, { user.FullName.Split(' ')[0] }! Est� na hora de tomar o seu rem�dio { Medicine }." },
                            To = message.From
                        }, DateTimeOffset.Parse(message.Content.ToString()), cancellationToken);
                        SState = "initialState";
                    }                    
                    break;
            } 

            //states:
            //cadastrarMedicamentos
            //cadastroPendente
            //estadoInicial

            //switch (["sessionState"])
            //{
            //    case "cadastrarMedicamentos":
            //        break;

            //    default:
            //        break;
            //}

            //await _bucket.SetAsync("users", _directory.GetDirectoryAccountAsync(message.From, cancellationToken));

            //await _sender.SendMessageAsync("Pong!", message.From, cancellationToken);

            //var newUser = new Command
            //{
            //    Method = CommandMethod.Set,
            //    Uri = new LimeUri("/contacts/" + message.From)
            //};

            //var response = await _sender.SendCommandAsync(newUser);

            //await _contacts.SetAsync(new Identity { Domain = message.From, Name = "message.Content.ToString()" }, new Lime.Messaging.Resources.Contact { Name = message.Content.ToString() }, cancellationToken);

            //var user = new Command
            //{
            //    Method = CommandMethod.Get,
            //    Uri = new LimeUri("/contacts/" + message.From)
            //};

            //await _sender.SendMessageAsync(new Resource { Key = "HelloWorld" }, message.From, cancellationToken);

            //await _sender.SendMessageAsync("Qual o seu nome?", message.From, cancellationToken);

            //var newUser = new Command
            //{
            //    Method = CommandMethod.Set,
            //    Uri = new LimeUri("/contacts/" + message.From)              
            //};

            //var response = await _sender.SendCommandAsync(newUser);

            //var user = new Command
            //{
            //    Method = CommandMethod.Get,
            //    Uri = new LimeUri("/contacts/" + message.From)
            //};

            //var response2 = await _sender.SendCommandAsync(user);

            //await _sender.SendMessageAsync("Prazer em conhec�-la  ${contact.name}", message.From, cancellationToken);

            //var command = new Command
            //{
            //    Method = CommandMethod.Get,
            //    Uri = new LimeUri("/account")
            //};

            //var response = await _sender.SendCommandAsync(command);

            //await _sender.SendMessageAsync("Pong!", message.From, cancellationToken);

            //await _sender.SendMessageAsync("Oi!", message.From, cancellationToken);
        }

        //{  
        //  "id": "1",
        //  "to": "postmaster@scheduler.msging.net",
        //  "method": "set",
        //  "uri": "/schedules",
        //  "type": "application/vnd.iris.schedule+json",
        //  "resource": {  
        //    "message": {  
        //      "id": "ad19adf8-f5ec-4fff-8aeb-2e7ebe9f7a67",
        //      "to": "destination@msging.net",
        //      "type": "text/plain",
        //      "content": "Teste agendamento"
        //    },
        //    "when": "2016-07-25T17:50:00.000Z"
        //  }
        //}

        //{  
        //  "id": "75c1621e-350c-4e85-8854-3e2cf3abbc3a",
        //  "to": "postmaster@scheduler.msging.net",
        //  "method": "get",
        //  "uri": "/schedules/ad19adf8-f5ec-4fff-8aeb-2e7ebe9f7a67"
        //}

        //{  
        //  "id": "1",
        //  "method": "set",
        //  "uri": "/contacts",
        //  "type": "application/vnd.lime.contact+json",
        //  "resource": {
        //    "identity": "11121023102013021@messenger.gw.msging.net",
        //    "name": "Jo�o da Silva",
        //    "gender":"male",
        //    "extras": {
        //      "plan":"Gold",
        //      "code":"1111"      
        //    }
        //  }
        //}

        //{  
        //  "id": "2",
        //  "method": "get",
        //  "uri": "/contacts/11121023102013021@messenger.gw.msging.net"
        //}

        //{  
        //  "id": "1",
        //  "to": "11121023102013021@messenger.gw.msging.net",
        //  "type": "text/plain",
        //  "content": "Ol�, ${contact.name}, seja bem vindo ao plano ${contact.extras.plan}!",
        //  "metadata": {
        //    "#message.replaceVariables": "true"
        //}
    }
}
