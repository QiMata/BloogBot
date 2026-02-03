#pragma once

#include <string>
#include <vector>
#include <unordered_map>
#include <memory>

namespace CodeIntelligence {

struct Symbol {
    std::string name;
    std::string type;           // function, class, variable, etc.
    std::string file;
    int line;
    int column;
    std::string signature;      // Full signature for functions
    std::string scope;          // Namespace or class scope
    std::vector<std::string> dependencies;
};

struct CompileCommand {
    std::string directory;
    std::string command;
    std::string file;
    std::vector<std::string> arguments;
};

class SymbolDatabase {
public:
    void addSymbol(const Symbol& symbol);
    std::vector<Symbol> findSymbol(const std::string& name) const;
    std::vector<Symbol> findSymbolsInFile(const std::string& file) const;
    std::vector<Symbol> findSymbolsByType(const std::string& type) const;
    
private:
    std::unordered_map<std::string, std::vector<Symbol>> symbolsByName;
    std::unordered_map<std::string, std::vector<Symbol>> symbolsByFile;
    std::unordered_map<std::string, std::vector<Symbol>> symbolsByType;
};

class CompileCommandsParser {
public:
    bool loadFromFile(const std::string& filePath);
    std::vector<CompileCommand> getCommands() const { return commands; }
    
private:
    std::vector<CompileCommand> commands;
};

class ASTAnalyzer {
public:
    std::vector<Symbol> analyzeFile(const std::string& filePath, 
                                   const CompileCommand& command);
    
private:
    std::vector<Symbol> parseClassDeclarations(const std::string& content, 
                                              const std::string& filePath);
    std::vector<Symbol> parseFunctionDeclarations(const std::string& content, 
                                                  const std::string& filePath);
    std::vector<Symbol> parseVariableDeclarations(const std::string& content, 
                                                  const std::string& filePath);
};

class MCPServer {
public:
    MCPServer(int port);
    ~MCPServer();
    
    void start();
    void stop();
    
private:
    void handleRequest(const std::string& request);
    std::string processSymbolQuery(const std::string& query);
    std::string processFileAnalysis(const std::string& filePath);
    std::string processCompileCommands();
    
    int port;
    bool running;
    std::unique_ptr<SymbolDatabase> symbolDb;
    std::unique_ptr<CompileCommandsParser> compileParser;
    std::unique_ptr<ASTAnalyzer> astAnalyzer;
};

} // namespace CodeIntelligence
