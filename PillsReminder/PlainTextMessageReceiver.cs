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

            if (msg.Equals("ok") || msg.Equals("obrigada") || msg.Equals("blz") || msg.Equals("vlw") || msg.Equals("até lá") || msg.Equals("até") 
                || msg.Equals("tchau") || msg.Equals("flw") )
            {
                await _sender.SendMessageAsync(":)", message.From);
            }

            switch (SState)
            {
                case "initialState":
                    await _sender.SendMessageAsync(new Select
                    {
                        Text = $"Olá, { user.FullName.Split(' ')[0] }! Bem-vindo(a) ao Pills Reminder. Eu vou lembrar você de tomar os seus medicamentos sempre no horário! Está pronto(a) para começar?",
                        Options = new[] {
                            new SelectOption {
                                Text = "Sim",
                                Value = new PlainText { Text = "Sim" }                                
                            },
                            new SelectOption
                            {
                                Text = "Não",
                                Value = new PlainText { Text = "Não" }
                            }
                        },
                        Scope = SelectScope.Immediate
                    },
                    message.From,
                    cancellationToken);
                    SState = "iniciado";
                    break;

                case "iniciado":
                    if (message.Content.ToString().Equals("Não"))
                    {
                        SState = "cadastroPendente";
                        await _sender.SendMessageAsync("Tudo bem! Podemos fazer depois então. Pode me chamar aqui quando quiser iniciar o cadastro. Até mais!", message.From);
                    }
                    else if(message.Content.ToString().Equals("Sim"))
                    {
                        SState = "cadastrarMedicamentos";
                        await _sender.SendMessageAsync("Oba! Então vamos começar. Qual o nome do seu medicamento?", message.From);
                    } else
                    {
                        SState = "iniciado";
                        await _sender.SendMessageAsync(new Select
                        {
                            Text = "Desculpe, não entendi. Você quer iniciar o cadasatro dos seus medicamentos agora?",
                            Options = new[] {
                                new SelectOption {
                                    Text = "Sim",
                                    Value = new PlainText { Text = "Sim" }
                                },
                                new SelectOption
                                {
                                    Text = "Não",
                                    Value = new PlainText { Text = "Não" }
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
                    await _sender.SendMessageAsync("Anotado! E que horas você quer que eu te lembre de tomá-lo?", message.From);
                    SState = "agendarHorario";
                    break;

                case "cadastroPendente":
                    await _sender.SendMessageAsync(new Select
                    {
                        Text = $"Olá, { user.FullName.Split(' ')[0] }! Bem-vindo(a) de volta. Está pronto(a) para começar?",
                        Options = new[] {
                            new SelectOption {
                                Text = "Sim",
                                Value = new PlainText { Text = "Sim" }
                            },
                            new SelectOption
                            {
                                Text = "Não",
                                Value = new PlainText { Text = "Não" }
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
                        await _sender.SendMessageAsync("Opa! Esse não me parece um horário válido. Escolha um horário entre 00:00 e 23:59. Como, por exemplo, 12:00 ou 18:30.", message.From);
                        SState = "agendarHorario";
                    }
                    else
                    {
                        await _sender.SendMessageAsync("Combinado! Pode ficar tranquilo(a) que vou te avisar quando chegar a hora! Até lá!", message.From);
                        await _scheduler.ScheduleMessageAsync(new Message
                        {
                            Id = Guid.NewGuid().ToString(),
                            From = message.To,
                            Content = new PlainText { Text = $"Olá, { user.FullName.Split(' ')[0] }! Está na hora de tomar o seu remédio { Medicine }." },
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

            //await _sender.SendMessageAsync("Prazer em conhecê-la  ${contact.name}", message.From, cancellationToken);

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
        //    "name": "João da Silva",
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
        //  "content": "Olá, ${contact.name}, seja bem vindo ao plano ${contact.extras.plan}!",
        //  "metadata": {
        //    "#message.replaceVariables": "true"
        //}
    }
}
