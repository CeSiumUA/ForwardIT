using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using TeleSharp.TL;
using TeleSharp.TL.Contacts;
using TeleSharp.TL.Messages;
using TeleSharp.TL.Updates;
using TLSharp.Core;
using TLSharp.Core.Utils;

namespace ForwardIT
{
    class TelegramViewModel : INotifyPropertyChanged
    {
        public event UnhandledExceptionEventHandler OnErrorReceived;
        public event EventHandler EventRaised;
        private TelegramClient telegramClient { get; set; }
        private TLUser telegramUser { get; set; }
        private CancellationTokenSource CancellationTokenSource { get; set; }
        public TLUser TelegramUser
        {
            get
            {
                return telegramUser;
            }
            set
            {
                if(telegramUser != value)
                {
                    telegramUser = value;
                    OnPropertyChanged("TelegramUser");
                }
            }
        }
        private string hash { get; set; }
        public event PropertyChangedEventHandler PropertyChanged;
        private string phoneNumber { get; set; }
        private string code { get; set; }
        private ICommand authCommand { get; set; }
        private ICommand listenerCommand { get; set; }
        private List<TLChannel> tlChannels { get; set; }
        private int authLevel { get; set; }
        private TLChannel selectedChannel { get; set; }
        private TLChannel selectedTargetChannel { get; set; }
        private string selectedUsers { get; set; }
        private Config cfg { get; set; }
        private string parsingStatus { get; set; }
        private string forwardStatus { get; set; }
        public string SelectedUsers
        {
            get
            {
                return selectedUsers;
            }
            set
            {
                if(selectedUsers != value)
                {
                    selectedUsers = value;
                    OnPropertyChanged("SelectedUsers");
                    SaveConfig();
                }
            }
        }
        public string ForwardStatus
        {
            get
            {
                return forwardStatus;
            }
            set
            {
                if(forwardStatus != value)
                {
                    forwardStatus = value;
                    OnPropertyChanged("ForwardStatus");
                }
            }
        }
        public string ParsingStatus
        {
            get
            {
                return parsingStatus;
            }
            set
            {
                if(parsingStatus != value)
                {
                    parsingStatus = value;
                    OnPropertyChanged("ParsingStatus");
                }
            }
        }
        public string[] SelectedUsersArray
        {
            get
            {
                if (!string.IsNullOrEmpty(SelectedUsers))
                {
                    return SelectedUsers.Replace(" ", string.Empty).Replace("\n", string.Empty).Replace("\r", string.Empty).Split(",");
                }
                else { return null; }
            }
            set
            {
                SelectedUsers = string.Join(",", value);
                OnPropertyChanged("SelectedUsers");
            }
        }
        public TLChannel SelectedChannel
        {
            get
            {
                return selectedChannel;
            }
            set
            {
                if(value != selectedChannel)
                {
                    selectedChannel = value;
                    OnPropertyChanged("SelectedChannel");
                    SaveConfig();
                }
            }
        }
        public TLChannel SelectedTargetChannel
        {
            get
            {
                return selectedTargetChannel;
            }
            set
            {
                if(value != selectedTargetChannel)
                {
                    selectedTargetChannel = value;
                    OnPropertyChanged("SelectedTargetChannel");
                    SaveConfig();
                }
            }
        }
        public List<TLChannel> Channels
        {
            get
            {
                return tlChannels;
            }
            set
            {
                if(tlChannels != value)
                {
                    tlChannels = value;
                    OnPropertyChanged("Channels");
                }
            }
        }
        public ICommand AuthCommand
        {
            get
            {
                return authCommand ??
                    (authCommand = new Command(async obj =>
                    {
                        await MakeAuthAsync();
                    }));
            }
        }
        public ICommand ListenerCommand
        {
            get
            {
                return listenerCommand ??
                    (listenerCommand = new Command(async obj =>
                    {
                        this.CancellationTokenSource.Cancel();
                        this.CancellationTokenSource = new CancellationTokenSource();
                        ParsingStatus = "Подготовка...";
                        EventRaised?.Invoke(this, new MessageEventArgs("Начата инициализация парсера!"));
                        await StartListenerAsync(CancellationTokenSource.Token);
                    }));
            }
        }
        public string PhoneNumber
        {
            get
            {
                return phoneNumber;
            }
            set
            {
                if(value != phoneNumber)
                {
                    phoneNumber = value;
                    OnPropertyChanged("PhoneNumber");
                }
            }
        }
        public string Code
        {
            get
            {
                return code;
            }
            set
            {
                if(value != code)
                {
                    code = value;
                    OnPropertyChanged("Code");
                }
            }
        }
        private async Task MakeAuthAsync()
        {
            
            if(authLevel == 0)
            {
                try
                {
                    
                    hash = await telegramClient.SendCodeRequestAsync(PhoneNumber);
                    authLevel++;
                }
                catch(Exception exc)
                {
                    OnErrorReceived?.Invoke(this, new UnhandledExceptionEventArgs(exc, false));
                    EventRaised?.Invoke(this, new MessageEventArgs("Неверный номер телефона!"));
                    try
                    {
                        File.Delete("session.dat");
                        telegramClient = new TelegramClient(Extensions.appid, Extensions.apphash);
                        await ConnectTelegram();
                    }
                    catch
                    {

                    }
                }
            }
            else if(authLevel == 1)
            {
                try
                {
                    telegramUser = await telegramClient.MakeAuthAsync(PhoneNumber, hash, Code);
                    authLevel = 0;
                    await LoadChats();
                }
                catch(Exception exc)
                {
                    authLevel = 2;
                    OnErrorReceived?.Invoke(this, new UnhandledExceptionEventArgs(exc, false));
                    EventRaised?.Invoke(this, new MessageEventArgs("Неправильный код, либо пользователь имеет Cloud Password! При наличии Cloud Password, введите его в поле код!"));
                }
            }
            else if(authLevel == 2)
            {
                try
                {
                    telegramUser = await telegramClient.MakeAuthWithPasswordAsync(await telegramClient.GetPasswordSetting(), Code);
                    authLevel = 0;
                    await LoadChats();
                }
                catch(Exception exc)
                {
                    OnErrorReceived?.Invoke(this, new UnhandledExceptionEventArgs(exc, false));
                    EventRaised?.Invoke(this, new MessageEventArgs("Не удалось аутентифицировать пользователя!"));
                }
            }
        }
        private async Task StartListenerAsync(CancellationToken cancellationToken)
        {
            List<TLUser> selectedUsers = new List<TLUser>();
            foreach (var userCallName in SelectedUsersArray)
            {
                try
                {
                    var usr = ((await telegramClient.SearchUserAsync(userCallName, 10)).Users.Cast<TLUser>().Where(x => x.Username == userCallName.Replace("@", string.Empty)).FirstOrDefault());
                    if (usr == null)
                    {
                        usr = (await telegramClient.GetContactsAsync()).Users.Cast<TLUser>().Where(x => x.Username == userCallName.Replace("@", string.Empty)).FirstOrDefault();
                        if (usr == null)
                        {
                            EventRaised?.Invoke(this, new MessageEventArgs($"Не удалось добавить пользователя {userCallName} в трэк лист!"));
                            continue;
                        }
                    }
                    ParsingStatus = $"Трэкинг {userCallName}...";
                    selectedUsers.Add(usr);
                }
                catch(Exception exc)
                {
                    OnErrorReceived?.Invoke(this, new UnhandledExceptionEventArgs(exc, false));
                    EventRaised?.Invoke(this, new MessageEventArgs("Не удалось начать парсинг! Пожалуйста, перезапустите приложение, или попробуйте ещё раз!"));
                    ParsingStatus = "Ошибка!";
                    //return;
                }
                await Task.Delay(100);
            }
            ParsingStatus = "Начат парсинг...";
            var inputPeerChannel = new TLInputPeerChannel()
            {
                ChannelId = SelectedChannel.Id,
                AccessHash = SelectedChannel.AccessHash.Value
            };
            var outputPeerChannel = new TLInputPeerChannel()
            {
                ChannelId = SelectedTargetChannel.Id,
                AccessHash = SelectedTargetChannel.AccessHash.Value
            };
            int lastSentId = 0;
            while (true && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    TLState offset = await telegramClient.SendRequestAsync<TLState>(new TLRequestGetState());
                    var req = new TLRequestGetDifference() { Date = offset.Date, Pts = offset.Pts, Qts = offset.Qts };
                    if (await telegramClient.SendRequestAsync<TLAbsDifference>(req) is TLDifference diff)
                    {
                        
                        foreach (var update in diff.OtherUpdates)
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                break;
                            }
                            try
                            {
                                if (update is TLUpdateNewChannelMessage messageUpdate)
                                {
                                    if (((messageUpdate.Message as TLMessage).ToId as TLPeerChannel).ChannelId == inputPeerChannel.ChannelId)
                                    {
                                        TLMessage incomeMessage = messageUpdate.Message as TLMessage;

                                        if (selectedUsers.Select(x => x.Id).Contains((int)incomeMessage.FromId))
                                        {
                                            #region CopyMessage
                                            //string txt = incomeMessage.Message;
                                            //if (incomeMessage.Media is TLMessageMediaPhoto mediaPhoto)
                                            //{
                                            //    var photoSize = (mediaPhoto.Photo as TLPhoto).Sizes.OfType<TLPhotoSize>().Last();
                                            //    TLFileLocation tf = photoSize.Location as TLFileLocation;
                                            //    var resFile = await telegramClient.GetFile(new TLInputFileLocation
                                            //    {
                                            //        LocalId = tf.LocalId,
                                            //        Secret = tf.Secret,
                                            //        VolumeId = tf.VolumeId,
                                            //    }, 0);
                                            //    using (FileStream fs = new FileStream(Environment.CurrentDirectory + @"\" + Guid.NewGuid().ToString() + ".jpg", FileMode.OpenOrCreate))
                                            //    {
                                            //        fs.Write(resFile.Bytes, 0, resFile.Bytes.Length);
                                            //    }
                                            //    txt = mediaPhoto.Caption;
                                            //}
                                            //try
                                            //{
                                            //    var curUsr = selectedUsers.Where(x => x.Id == (int)incomeMessage.FromId).FirstOrDefault();
                                            //    txt = $"[{curUsr.FirstName} {curUsr.LastName}]\n" + txt;
                                            //}
                                            //catch
                                            //{

                                            //}
                                            //var t = await telegramClient.SendMessageAsync(outputPeerChannel, txt);
                                            #endregion
                                            #region ForwardMessage
                                            if (lastSentId != incomeMessage.Id)
                                            {
                                                var randomIds = new TLVector<long>
                                                {
                                                    TLSharp.Core.Utils.Helpers.GenerateRandomLong()
                                                };
                                                TLVector<int> tlvctint = new TLVector<int>();
                                                tlvctint.Add(incomeMessage.Id);
                                                var forward = new TLRequestForwardMessages()
                                                {
                                                    FromPeer = inputPeerChannel,
                                                    ToPeer = outputPeerChannel,
                                                    Id = tlvctint,
                                                    RandomId = randomIds
                                                };
                                                var user = selectedUsers.Where(x => x.Id == incomeMessage.FromId).FirstOrDefault();
                                                ForwardStatus = $"[{DateTime.Now}] Форвард от: {user.FirstName} {user.LastName}";
                                                var tlUpdates = await telegramClient.SendRequestAsync<TLUpdates>(forward);
                                            }
                                            lastSentId = incomeMessage.Id;
                                            #endregion
                                        }
                                    }
                                }
                            }
                            catch (Exception exc)
                            {
                                OnErrorReceived?.Invoke(this, new UnhandledExceptionEventArgs(exc, false));
                            }
                            offset.Pts++;
                        }
                        
                    }
                    
                    await Task.Delay(100);
                }
                catch(Exception exc)
                {
                    OnErrorReceived?.Invoke(this, new UnhandledExceptionEventArgs(exc, false));
                }

            }
            ParsingStatus = "Завершено!";
        }
        private async Task ParseConfig()
        {
            try
            {
                cfg = new Config();
                using (StreamReader sr = new StreamReader(@"conf\parserConfig.json"))
                {
                    cfg = Newtonsoft.Json.JsonConvert.DeserializeObject<Config>(await sr.ReadToEndAsync());
                }
                SelectedChannel = Channels.Where(x => x.Title == cfg.InputChannelHeader).FirstOrDefault();
                SelectedTargetChannel = Channels.Where(x => x.Title == cfg.OutputChannelHeader).FirstOrDefault();
                SelectedUsersArray = cfg.SelectedUsers.ToArray();
            }
            catch(Exception ec)
            {

            }
        }
        private async Task SaveConfig()
        {
            if(cfg == null)
            {
                cfg = new Config();
            }
            if (SelectedUsersArray != null)
            {
                cfg.SelectedUsers = SelectedUsersArray.ToList();
            }
            if (SelectedChannel != null)
            {
                cfg.InputChannelHeader = SelectedChannel.Title;
            }
            if (SelectedTargetChannel != null)
            {
                cfg.OutputChannelHeader = SelectedTargetChannel.Title;
            }
            var dir = Directory.CreateDirectory("conf");
            using(StreamWriter sw = new StreamWriter(@"conf\parserConfig.json"))
            {
                await sw.WriteLineAsync(Newtonsoft.Json.JsonConvert.SerializeObject(cfg));
            }
        }
        public TelegramViewModel()
        {
            InitTelegram();
        }
        private async Task InitTelegram()
        {
            CancellationTokenSource = new CancellationTokenSource();
            Channels = new List<TLChannel>();
            authLevel = 0;
            telegramClient = new TelegramClient(Extensions.appid, Extensions.apphash);
            await ConnectTelegram();
            TelegramUser = telegramClient.Session.TLUser;
            await LoadChats();
            await ParseConfig();
        }
        private async Task ConnectTelegram()
        {
            await telegramClient.ConnectAsync();
        }
        private async Task LoadChats()
        {
            try
            {
                TLVector<TLAbsChat> chats = new TLVector<TLAbsChat>();
                try
                {
                    var dialogs = (TLDialogsSlice)await telegramClient.GetUserDialogsAsync();
                    chats = dialogs.Chats;
                }
                catch
                {
                    var dialogs = (TLDialogs)await telegramClient.GetUserDialogsAsync();
                    chats = dialogs.Chats;
                }
                finally
                {
                    foreach(var chat in chats)
                    {
                        if(chat is TLChannel)
                        {
                            Channels.Add(chat as TLChannel);
                        }
                    }
                }
            }
            catch(Exception exc)
            {

            }
        }
        private async Task OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    public class Command : ICommand
    {
        private Action<object> execute;
        private Func<object, bool> canExecute;
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
        public bool CanExecute(object parameter)
        {
            return this.canExecute == null || this.canExecute(parameter);
        }
        public Command(Action<object> execute, Func<object, bool> canExecute = null)
        {
            this.execute = execute;
            this.canExecute = canExecute;
        }
        public void Execute(object parameter)
        {
            this.execute(parameter);
        }
    }
    public static class Extensions
    {
        public const int appid = 1370246;
        public const string apphash = "98fcffe609959ede658be63722c45b76";
    }
    public class MessageEventArgs : EventArgs
    {
        public MessageEventArgs(string Message)
        {
            this.Message = Message;
        }
        public string Message { get; set; }
    }
    public class Config
    {
        public List<string>? SelectedUsers { get; set; }
        public string? InputChannelHeader { get; set; }
        public string? OutputChannelHeader { get; set; }
    }
}
