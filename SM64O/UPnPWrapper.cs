﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;
using Mono.Nat;

namespace SM64O
{
    public class UPnPWrapper
    {
        public static List<INatDevice> Devices = new List<INatDevice>();
        public event EventHandler Available;

        private List<Mapping> _mappings = new List<Mapping>();
        private string _cachedIp;

        public UPnPWrapper()
        {
            NatUtility.DeviceFound += NatUtilityOnDeviceFound;
            NatUtility.DeviceLost += NatUtilityOnDeviceLost;
        }

        public bool UPnPAvailable
        {
            get { return Devices.Count > 0; }
        }

        public string GetExternalIp()
        {
            if (!string.IsNullOrEmpty(_cachedIp)) return _cachedIp;
            if (!UPnPAvailable) return null;
            
            INatDevice dev = Devices[0];

            string external = null;

            try
            {
                var ip = dev.GetExternalIP();
                if (ip == null)
                    throw new NullReferenceException();
                external = ip.ToString();
            }
            catch (NullReferenceException)
            {
                return null;
            }

            return _cachedIp = external;
        }

        public bool AddPortRule(int port, bool tcp, string desc)
        {
            if (!UPnPAvailable) return false;

            try
            {
                Mapping map = new Mapping(tcp ? Protocol.Tcp : Protocol.Udp, port, port);
                map.Description = desc;

                Devices[0].CreatePortMap(map);
                _mappings.Add(map);
                return true;
            }
            catch (MappingException ex)
            {
                Program.LogException(ex);
                return false;
            }
        }

        public bool RemovePortRule(int port)
        {
            if (!UPnPAvailable) return false;

            foreach (Mapping mapping in Devices[0].GetAllMappings())
            {
                if (mapping.PrivatePort == mapping.PublicPort && mapping.PublicPort == port)
                    Devices[0].DeletePortMap(mapping);
            }
            
            return true;
        }

        public void RemoveOurRules()
        {
            if (!UPnPAvailable) return;

            foreach (var mapping in _mappings)
            {
                try
                {
                    Devices[0].DeletePortMap(mapping);
                }
                catch (MappingException ex)
                {
                    Program.LogException(ex);
                }
            }

            _mappings.Clear();
        }

        public void StopDiscovery()
        {
            NatUtility.StopDiscovery();

            NatUtility.DeviceLost -= NatUtilityOnDeviceLost;
            NatUtility.DeviceFound -= NatUtilityOnDeviceFound;
        }

        public void Initialize()
        {
            Task.Run(() =>
            {
                try
                {
                    NatUtility.StartDiscovery();
                }
                catch { }
                // Swallow NAT exceptions
            });
        }

        private void NatUtilityOnDeviceLost(object sender, DeviceEventArgs deviceEventArgs)
        {
            Devices.Remove(deviceEventArgs.Device);
        }

        private void NatUtilityOnDeviceFound(object sender, DeviceEventArgs deviceEventArgs)
        {
            Devices.Add(deviceEventArgs.Device);

            if (Devices.Count == 1) {} // TODO this didn't want to compile for me
                // Available?.Invoke(this, EventArgs.Empty);
        }
    }
}