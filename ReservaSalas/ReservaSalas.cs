using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Linq;

namespace ReservaSalas
{
    public class Sala
    {
        public int Id { get; set; }
        public int Capacidade { get; set; }
        public bool TemInternet { get; set; }
        public bool TemTvWebcam { get; set; }
    }

    public class Reserva
    {
        public string Id { get; set; }
        public int Sala { get; set; }
        public DateTime Inicio { get; set; }
        public DateTime Fim { get; set; }
    }

    public class Banco
    {
        public List<Sala> Salas { get; set; }
        public List<Reserva> Reservas { get; set; }
    }

    class ReservaSalas
    {
        static void Main(string[] args)
        {
            Banco banco = Arquivo.Ler();
            var sair = false;
            do
            {
                Console.WriteLine("Digite os dados para reserva da sala ou 's' para sair.");
                var cmd = Console.ReadLine();
                if (cmd.ToLower() == "sair" || cmd.ToLower() == "s")
                {
                    sair = true;
                }
                else
                {
                    try
                    {
                        var comando = cmd.Split(';');
                        var dataInicio = comando[0].Split('-');
                        var horaInicio = comando[1].Split(':');
                        var dataHoraInicio = new DateTime(
                            year: Convert.ToInt32(dataInicio[2]),
                            month: Convert.ToInt32(dataInicio[1]),
                            day: Convert.ToInt32(dataInicio[0]),
                            hour: Convert.ToInt32(horaInicio[0]),
                            minute: Convert.ToInt32(horaInicio[1]),
                            second: 0);

                        var dataFim = comando[2].Split('-');
                        var horaFim = comando[3].Split(':');
                        var dataHoraFim = new DateTime(
                            year: Convert.ToInt32(dataFim[2]),
                            month: Convert.ToInt32(dataFim[1]),
                            day: Convert.ToInt32(dataFim[0]),
                            hour: Convert.ToInt32(horaFim[0]),
                            minute: Convert.ToInt32(horaFim[1]),
                            second: 0);

                        var quantidadePessoas = Convert.ToInt32(comando[4]);
                        var acessoInternet = comando[5] == "Sim";
                        var tvWebcam = comando[6] == "Sim";

                        var dataHoraAtual = DateTime.Now;

                        if (dataHoraInicio >= dataHoraFim) throw new InvalidOperationException("A data e hora de início deve ser menor que a data e hora final.");

                        if (dataHoraFim > dataHoraInicio.AddHours(8)) throw new InvalidOperationException("A utilização da Sala não pode ser maior que 8 horas.");

                        if (dataHoraInicio > dataHoraAtual.AddDays(40)) throw new InvalidOperationException("Não é permitido agendar com mais de 40 dias de antecedencia.");

                        if (dataHoraInicio < dataHoraAtual.AddDays(1)) throw new InvalidOperationException("Não é permitido agendar com menos de 1 dia de antecedencia.");

                        if (dataHoraInicio.DayOfWeek == DayOfWeek.Saturday || dataHoraInicio.DayOfWeek == DayOfWeek.Sunday) throw new InvalidOperationException("Somente é permitido agendar em dias úteis");

                        var salaEscolhida = Pesquisar(banco, quantidadePessoas, acessoInternet, tvWebcam, dataHoraInicio, dataHoraFim);

                        if (salaEscolhida > 0)
                        {
                            
                            banco.Reservas.Add(new Reserva() { Id = Guid.NewGuid().ToString("N"), Sala = salaEscolhida, Inicio = dataHoraInicio, Fim = dataHoraFim });
                            Arquivo.Escrever(banco);
                            Console.WriteLine("Sala " + salaEscolhida);
                        }
                        else
                        {
                            Console.WriteLine("Essa data não está disponível");
                            var i = 1;
                            var j = 0;
                            var sugestao = 0;
                            do
                            {
                                j++;
                                if (dataHoraInicio.AddDays(j).DayOfWeek == DayOfWeek.Saturday) j += 2;
                                sugestao = Pesquisar(banco, quantidadePessoas, acessoInternet, tvWebcam, dataHoraInicio.AddDays(j), dataHoraFim.AddDays(j));
                                if (sugestao > 0)
                                {
                                    string s = String.Format("Início: {0:d} {0:t}; Fim: {1:d} {1:t}; Sala: {2}",
                                        dataHoraInicio.AddDays(j),
                                        dataHoraFim.AddDays(j),
                                        sugestao);
                                    Console.WriteLine("Sugestão " + i + "=> " + s);
                                    i++;
                                }
                            } while (i <= 3);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Erro: Entradas inválidas. (" + ex.Message + ")");
                    }
                }
            } while (!sair);
        }

        static int Pesquisar(Banco banco, int quantidadePessoas, bool acessoInternet, bool tvWebcam, DateTime dataHoraInicio, DateTime dataHoraFim)
        {
            var listaSalasDisponiveis = banco.Salas.Where(a => a.Capacidade >= quantidadePessoas && a.TemInternet == acessoInternet && a.TemTvWebcam == tvWebcam).Select(a => a);
            foreach (var item in listaSalasDisponiveis)
            {
                var horarioDisponivel = banco.Reservas.Where(a => (dataHoraInicio < a.Fim && dataHoraFim > a.Inicio) && a.Sala == item.Id).Select(a => a);

                if (horarioDisponivel.Count() == 0)
                {
                    return item.Id;
                }
            }
            return -1;
        }
    }

    public static class ExtensaoString
    {
        public static string ParseHome(this string path)
        {
            string home = (Environment.OSVersion.Platform == PlatformID.Unix ||
                Environment.OSVersion.Platform == PlatformID.MacOSX)
                ? Environment.GetEnvironmentVariable("HOME")
                : Environment.ExpandEnvironmentVariables("%HOMEDRIVE%%HOMEPATH%");
            return path.Replace("~", home);
        }
    }

    class Arquivo
    {
        private static string path = @"~/ReservaSalas.json".ParseHome();
        public static Banco Ler()
        {
            Banco banco = new Banco
            {
                Salas = new List<Sala> { },
                Reservas = new List<Reserva> { }
            };

            if (!File.Exists(path))
            {

                for (int i = 0; i < 12; i++)
                {
                    if (i >= 0 && i < 5)
                    {
                        banco.Salas.Add(new Sala() { Id = i + 1, Capacidade = 10, TemInternet = true, TemTvWebcam = true });
                    }
                    else if (i > 4 && i < 7)
                    {
                        banco.Salas.Add(new Sala() { Id = i + 1, Capacidade = 10, TemInternet = true, TemTvWebcam = false });
                    }
                    else if (i > 6 && i < 10)
                    {
                        banco.Salas.Add(new Sala() { Id = i + 1, Capacidade = 3, TemInternet = true, TemTvWebcam = true });
                    }
                    else
                    {
                        banco.Salas.Add(new Sala() { Id = i + 1, Capacidade = 20, TemInternet = false, TemTvWebcam = false });
                    }
                }
                Escrever(banco);
            }

            try
            {
                using StreamReader sr = new StreamReader(path);
                var texto = sr.ReadToEnd();
                banco = JsonSerializer.Deserialize<Banco>(texto);
                sr.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return banco;
        }

        public static void Escrever(Banco banco)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
            };
            var jsonString = JsonSerializer.Serialize(banco, options);
            using StreamWriter sw = File.CreateText(path);
            sw.Write(jsonString);
            sw.Close();
        }
    }
}
