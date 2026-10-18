import socket,json,struct,sys
from pathlib import Path

def call(kind,params):
 port=json.loads(Path('Temp/.com-unity-codely.json').read_text())['unity_port']
 with socket.create_connection(('127.0.0.1',port),5) as s:
  s.settimeout(90);banner=b''
  while not banner.endswith(b'\n'):banner+=s.recv(1)
  data=json.dumps({'type':kind,'params':params}).encode();s.sendall(struct.pack('>Q',len(data))+data)
  def exact(n):
   b=b''
   while len(b)<n:
    v=s.recv(n-len(b))
    if not v:raise RuntimeError('EOF '+repr(b))
    b+=v
   return b
  n=struct.unpack('>Q',exact(8))[0]
  if n>64000000:raise RuntimeError('invalid length '+str(n))
  return json.loads(exact(n))
if __name__=='__main__':
 if sys.argv[1]=='code':print(json.dumps(call('execute_csharp_script',{'script':Path(sys.argv[2]).read_text()}),ensure_ascii=False))
 else:print(json.dumps(call(sys.argv[1],json.loads(sys.argv[2])),ensure_ascii=False))
