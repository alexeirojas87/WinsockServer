using System;
using System.Collections;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WinSockServer
{
    public class WinSockServer
    {

        #region "ESTRUCTURAS"
        private struct InfoDeUnCliente
        {
            public Socket Socket;
            public Task Task;
            public string UltimosDatosRecibidos;
        }
        #endregion

        #region "VARIABLES"
        private TcpListener _tcpLsn;
        private Hashtable _clientes = new Hashtable();
        private Thread _tcpThd;
        private IPEndPoint _idClienteActual;
        #endregion

        #region "EVENTOS"
        public event NuevaConexionEventHandler NuevaConexion;
        public delegate void NuevaConexionEventHandler(IPEndPoint idTerminal);
        public event DatosRecibidosEventHandler DatosRecibidos;
        public delegate void DatosRecibidosEventHandler(IPEndPoint idTerminal);
        public event ConexionTerminadaEventHandler ConexionTerminada;
        public delegate void ConexionTerminadaEventHandler(IPEndPoint idTerminal);

        #endregion

        #region "PROPIEDADES"
        public string PuertoDeEscucha { get; set; }
        public int LengBufferSocket { get; set; } = 102;

        #endregion

        #region "METODOS"

        public void Escuchar()
        {
            IPAddress ipAddress = GetLocalIP();
            if (ipAddress != null)
            {
                _tcpLsn = new TcpListener(ipAddress, Convert.ToInt32(PuertoDeEscucha));
                _tcpLsn.Start();
                Task.Run((() => EsperarCliente()));
            }

        }

        private IPAddress GetLocalIP()
        {
            IPAddress localAddress = null;
            NetworkInterface[] networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (var network in networkInterfaces)
            {
                IPInterfaceProperties properties = network.GetIPProperties();
                if (network.NetworkInterfaceType == NetworkInterfaceType.Ethernet &&
                    network.OperationalStatus == OperationalStatus.Up &&
                    !network.Description.ToLower().Contains("virtual") &&
                    !network.Description.ToLower().Contains("pseudo") &&
                    !network.Description.ToLower().Contains("vpn"))
                {
                    foreach (IPAddressInformation address in properties.UnicastAddresses)
                    {
                        if (address.Address.AddressFamily != AddressFamily.InterNetwork)
                            continue;
                        if (IPAddress.IsLoopback(address.Address))
                            continue;
                        localAddress = address.Address;
                    }
                }
            }

            return localAddress;
        }

        public string ObtenerDatos(IPEndPoint idCliente)
        {
            InfoDeUnCliente infoClienteSolicitado = (InfoDeUnCliente)_clientes[idCliente];
            return infoClienteSolicitado.UltimosDatosRecibidos;
        }

        public void Cerrar(IPEndPoint idCliente)
        {
            InfoDeUnCliente infoClienteActual = (InfoDeUnCliente)_clientes[idCliente];
            //Cierro la conexion con el cliente
            infoClienteActual.Task.Dispose();
            infoClienteActual.Socket.Close();
            _clientes.Remove(infoClienteActual);
        }

        public void Cerrar()
        {
            foreach (InfoDeUnCliente item in _clientes.Values)
            {
                Cerrar((IPEndPoint)item.Socket.RemoteEndPoint);
                _clientes.Remove(item);
            }

        }

        public void EnviarDatos(IPEndPoint idCliente, string datos)
        {
            InfoDeUnCliente cliente = (InfoDeUnCliente)_clientes[idCliente];
            cliente.Socket.Send(Encoding.ASCII.GetBytes(datos));
        }

        public void EnviarDatos(string datos)
        {
            foreach (InfoDeUnCliente item in _clientes.Values)
            {
                EnviarDatos((IPEndPoint)item.Socket.RemoteEndPoint, datos);
            }
        }



        #endregion

        #region "FUNCIONES PRIVADAS"
        private void EsperarCliente()
        {
            while (true)
            {
                var cliente = default(InfoDeUnCliente);
                cliente.Socket = _tcpLsn.AcceptSocket();
                _idClienteActual = (IPEndPoint)cliente.Socket.RemoteEndPoint;
                _clientes.Add(_idClienteActual, cliente);
                NuevaConexion?.Invoke(_idClienteActual);
                cliente.Task = Task.Run((() => LeerSocket()));
            }

        }

        private void LeerSocket()
        {
            IPEndPoint idReal = _idClienteActual;
            var with2 = (InfoDeUnCliente)_clientes[idReal]; ;

            while (true)
            {
                var recibir = new byte[LengBufferSocket];
                try
                {
                    var ret = with2.Socket.Receive(recibir, recibir.Length, SocketFlags.None);

                    if (ret > 0)
                    {
                        with2.UltimosDatosRecibidos = BitConverter.ToString(recibir);

                        _clientes[idReal] = with2;
                        DatosRecibidos?.Invoke(idReal);
                    }
                    else
                    {
                        ConexionTerminada?.Invoke(idReal);
                        break;
                    }
                }
                catch (Exception e)
                {
                    if (!with2.Socket.Connected)
                    {
                        ConexionTerminada?.Invoke(idReal);
                        break;
                    }
                }
            }

            CerrarThread(idReal);
        }

        private void CerrarThread(IPEndPoint idCliente)
        {
                InfoDeUnCliente infoClienteActual = (InfoDeUnCliente)_clientes[idCliente];
                try
                {
                    infoClienteActual.Task.Dispose();
                    _clientes.Remove(idCliente);
                }
                catch (Exception e)
                {
                    _clientes.Remove(idCliente);
                }
        }



        #endregion

    }


}
